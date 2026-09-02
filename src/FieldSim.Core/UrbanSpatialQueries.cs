namespace FieldSim.Core;

/// <summary>
/// Meter-native helpers for synthetic urban geometry. These routines intentionally operate on
/// fictional scenario structures rather than importing exact real building geometry.
/// </summary>
public static class UrbanSpatialQueries
{
    public static bool PointInsideStructure(TacticalState state, Position3D point)
    {
        if (state.World.TryExternalPointInsideStructure(point, out var externalInside))
            return externalInside;

        return state.Structures.Any(structure => FootprintContains(structure.Bounds, point.X, point.Y));
    }

    public static StructureVolume? FirstMovementBlocker(TacticalState state, Position3D from, Position3D to) =>
        state.Structures.FirstOrDefault(structure =>
            !FootprintContains(structure.Bounds, from.X, from.Y) &&
            !FootprintContains(structure.Bounds, to.X, to.Y) &&
            SegmentIntersectsFootprint(from, to, structure.Bounds));

    public static StructureVolume? FirstSightBlocker(TacticalState state, Position3D from, Position3D to) =>
        state.Structures.FirstOrDefault(structure =>
            !structure.Bounds.Contains(from) && !structure.Bounds.Contains(to) &&
            SegmentIntersectsBounds3D(from, to, structure.Bounds));

    public static bool SegmentBlockedForMovement(TacticalState state, Position3D from, Position3D to) =>
        SegmentBlockedForMovement(state, from, to, TacticalUnitClass.Person);

    public static bool SegmentBlockedForMovement(
        TacticalState state,
        Position3D from,
        Position3D to,
        TacticalUnitClass unitClass)
    {
        if (state.World.TryExternalMovementBlock(from, to, unitClass, out var externalBlocked))
            return externalBlocked;

        return FirstMovementBlocker(state, from, to) is not null;
    }

    public static bool IsWalkablePoint(TacticalState state, TacticalUnit unit, Position3D point)
    {
        var maxX = state.Width * state.World.CellSizeMeters;
        var maxY = state.Height * state.World.CellSizeMeters;
        if (!double.IsFinite(point.X) || !double.IsFinite(point.Y) ||
            point.X < 0 || point.Y < 0 || point.X >= maxX || point.Y >= maxY)
            return false;

        var cell = state.World.GridPointFromWorld(point.X, point.Y);
        if (state.World.TryExternalWalkability(point, unit.UnitClass, out var externalWalkable))
            return externalWalkable && state.Infrastructure.IsTraversable(cell, unit.UnitClass);

        if (PointInsideStructure(state, point)) return false;
        return TacticalEngine.IsWalkable(state.Tiles[cell.X, cell.Y]) &&
               state.Infrastructure.IsTraversable(cell, unit.UnitClass);
    }

    public static double CoverScoreAt(TacticalState state, Position3D point)
    {
        var context = state.World.ContextAt(point.X, point.Y);
        var score = context.Cover * 0.75 + context.Concealment * 0.35;
        var nearestWall = state.Structures
            .Select(structure => DistanceToFootprint(point, structure.Bounds))
            .DefaultIfEmpty(double.PositiveInfinity)
            .Min();
        if (nearestWall <= 2.5) score += 0.72;
        else if (nearestWall <= 5.0) score += 0.50;
        else if (nearestWall <= 9.0) score += 0.24;
        return Math.Clamp(score, 0, 1.5);
    }

    public static bool TryFindDetour(
        TacticalState state,
        TacticalUnit unit,
        Position3D from,
        Position3D to,
        out IReadOnlyList<Position3D> waypoints)
    {
        if (state.World.TryExternalPath(from, to, unit.UnitClass, out var externalPath))
        {
            waypoints = externalPath;
            return true;
        }

        var result = new List<Position3D>();
        var cursor = from;
        const double clearance = 5.0;

        for (var iteration = 0; iteration < 8; iteration++)
        {
            var blocker = FirstMovementBlocker(state, cursor, to);
            if (blocker is null)
            {
                waypoints = result;
                return result.Count > 0;
            }

            var bounds = blocker.Bounds;
            var candidates = new[]
            {
                new Position3D(bounds.Minimum.X - clearance, bounds.Minimum.Y - clearance, 0),
                new Position3D(bounds.Minimum.X - clearance, bounds.Maximum.Y + clearance, 0),
                new Position3D(bounds.Maximum.X + clearance, bounds.Minimum.Y - clearance, 0),
                new Position3D(bounds.Maximum.X + clearance, bounds.Maximum.Y + clearance, 0)
            }
            .Select(point => point with { Z = state.World.GroundAltitudeAt(point.X, point.Y) })
            .Where(point => IsWalkablePoint(state, unit, point))
            .Where(point => FirstMovementBlocker(state, cursor, point) is null)
            .OrderBy(point => cursor.HorizontalDistanceTo(point) + point.HorizontalDistanceTo(to))
            .ToArray();

            if (candidates.Length == 0)
            {
                waypoints = Array.Empty<Position3D>();
                return false;
            }

            var chosen = candidates[0];
            if (result.Count > 0 && chosen.HorizontalDistanceTo(result[^1]) < 0.5)
            {
                waypoints = Array.Empty<Position3D>();
                return false;
            }
            result.Add(chosen);
            cursor = chosen;
        }

        waypoints = Array.Empty<Position3D>();
        return false;
    }

    private static bool FootprintContains(AxisAlignedBounds3D bounds, double x, double y) =>
        x >= bounds.Minimum.X && x <= bounds.Maximum.X &&
        y >= bounds.Minimum.Y && y <= bounds.Maximum.Y;

    private static double DistanceToFootprint(Position3D point, AxisAlignedBounds3D bounds)
    {
        var dx = point.X < bounds.Minimum.X ? bounds.Minimum.X - point.X :
            point.X > bounds.Maximum.X ? point.X - bounds.Maximum.X : 0;
        var dy = point.Y < bounds.Minimum.Y ? bounds.Minimum.Y - point.Y :
            point.Y > bounds.Maximum.Y ? point.Y - bounds.Maximum.Y : 0;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static bool SegmentIntersectsFootprint(Position3D from, Position3D to, AxisAlignedBounds3D bounds)
    {
        var tMin = 0.0;
        var tMax = 1.0;
        if (!ClipAxis(from.X, to.X - from.X, bounds.Minimum.X, bounds.Maximum.X, ref tMin, ref tMax)) return false;
        if (!ClipAxis(from.Y, to.Y - from.Y, bounds.Minimum.Y, bounds.Maximum.Y, ref tMin, ref tMax)) return false;
        return tMax >= tMin && tMax >= 0 && tMin <= 1;
    }

    private static bool SegmentIntersectsBounds3D(Position3D from, Position3D to, AxisAlignedBounds3D bounds)
    {
        var tMin = 0.0;
        var tMax = 1.0;
        if (!ClipAxis(from.X, to.X - from.X, bounds.Minimum.X, bounds.Maximum.X, ref tMin, ref tMax)) return false;
        if (!ClipAxis(from.Y, to.Y - from.Y, bounds.Minimum.Y, bounds.Maximum.Y, ref tMin, ref tMax)) return false;
        if (!ClipAxis(from.Z, to.Z - from.Z, bounds.Minimum.Z, bounds.Maximum.Z, ref tMin, ref tMax)) return false;
        return tMax >= tMin && tMax >= 0 && tMin <= 1;
    }

    private static bool ClipAxis(double start, double delta, double minimum, double maximum, ref double tMin, ref double tMax)
    {
        if (Math.Abs(delta) < 1e-9)
            return start >= minimum && start <= maximum;

        var first = (minimum - start) / delta;
        var second = (maximum - start) / delta;
        if (first > second) (first, second) = (second, first);
        tMin = Math.Max(tMin, first);
        tMax = Math.Min(tMax, second);
        return tMin <= tMax;
    }
}
