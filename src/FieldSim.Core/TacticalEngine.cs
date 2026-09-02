namespace FieldSim.Core;

public static class TacticalEngine
{
    private static readonly GridPoint[] Directions =
    [
        new(0, -1), new(-1, 0), new(1, 0), new(0, 1),
        new(-1, -1), new(1, -1), new(-1, 1), new(1, 1)
    ];

    public static bool IsWalkable(TacticalTile tile) =>
        tile is not TacticalTile.Building and not TacticalTile.Water;

    // Relative path-planning cost only. Movement itself is continuous in meters.
    public static int MoveCost(TacticalTile tile) => tile switch
    {
        TacticalTile.Road => 4,
        TacticalTile.Open => 6,
        TacticalTile.Agricultural => 7,
        TacticalTile.Scrub => 8,
        TacticalTile.Rocky => 9,
        TacticalTile.Forest => 10,
        _ => int.MaxValue
    };

    public static double NominalSpeedMetersPerSecond(TacticalUnit unit) => unit.Mobility.MaxSpeedMetersPerSecond;

    public static int UnitAt(TacticalState state, int x, int y)
    {
        EnsureIndexes(state);
        var center = state.World.CellCenter(new GridPoint(x, y), 0);
        var radius = state.World.CellSizeMeters * 0.72;
        var nearby = state.SpatialIndex.QueryRadius(center, radius);
        state.Performance.RecordSpatialQuery(nearby.Count);
        foreach (var id in nearby)
        {
            var unit = state.UnitById(id);
            if (unit is null || !unit.Alive || unit.Position != new GridPoint(x, y)) continue;
            return state.Units.IndexOf(unit);
        }
        return -1;
    }

    public static IReadOnlyList<TacticalUnit> UnitsNear(TacticalState state, Position3D center, double radiusMeters)
    {
        EnsureIndexes(state);
        var result = new List<TacticalUnit>();
        var nearby = state.SpatialIndex.QueryRadius(center, radiusMeters);
        state.Performance.RecordSpatialQuery(nearby.Count);
        foreach (var id in nearby)
        {
            var unit = state.UnitById(id);
            if (unit is not null && unit.Alive) result.Add(unit);
        }
        return result;
    }

    public static int SelectAt(TacticalState state, int x, int y,
        TacticalFaction? selectableFaction = TacticalFaction.Blue)
    {
        foreach (var unit in state.Units) unit.Selected = false;
        var index = UnitAt(state, x, y);
        if (index >= 0 &&
            (selectableFaction is null || state.Units[index].Faction == selectableFaction))
        {
            state.Units[index].Selected = true;
            return index;
        }
        return -1;
    }

    public static bool IssueMove(TacticalState state, int unitIndex, GridPoint destination)
    {
        if (unitIndex < 0 || unitIndex >= state.Units.Count || !state.Units[unitIndex].Alive)
            return false;
        if (!state.InBounds(destination.X, destination.Y)) return false;

        var exactDestination = state.World.CellCenter(destination, 0);
        return IssueMoveInternal(state, unitIndex, destination, exactDestination);
    }

    public static bool IssueMoveMeters(TacticalState state, int unitIndex, Position3D destinationMeters)
    {
        if (unitIndex < 0 || unitIndex >= state.Units.Count || !state.Units[unitIndex].Alive)
            return false;
        var maxX = state.Width * state.World.CellSizeMeters;
        var maxY = state.Height * state.World.CellSizeMeters;
        if (!double.IsFinite(destinationMeters.X) || !double.IsFinite(destinationMeters.Y) ||
            destinationMeters.X < 0 || destinationMeters.Y < 0 ||
            destinationMeters.X >= maxX || destinationMeters.Y >= maxY)
            return false;
        var goal = state.World.GridPointFromWorld(destinationMeters.X, destinationMeters.Y);
        var exact = destinationMeters with { Z = state.World.GroundAltitudeAt(destinationMeters.X, destinationMeters.Y) };
        return IssueMoveInternal(state, unitIndex, goal, exact);
    }

    private static bool IssueMoveInternal(TacticalState state, int unitIndex, GridPoint goal, Position3D exactDestination)
    {
        var unit = state.Units[unitIndex];
        state.EnsurePrecisePositions();
        if (!IsWalkable(state.Tiles[goal.X, goal.Y]) || !state.Infrastructure.IsTraversable(goal, unit.UnitClass))
            return false;

        // v1.10: physical movement stays meter-native. Synthetic urban structures can create
        // short continuous detours; the coarse country planning lattice is only a final compatibility
        // fallback for larger blocked terrain regions.
        if (UrbanSpatialQueries.PointInsideStructure(state, exactDestination)) return false;

        var start = state.GroundPositionOf(unit);
        List<GridPoint>? path = [];
        IReadOnlyList<Position3D> continuousDetour = Array.Empty<Position3D>();
        if (!StraightLineTraversable(state, unit, start, exactDestination))
        {
            if (UrbanSpatialQueries.FirstMovementBlocker(state, start, exactDestination) is not null &&
                UrbanSpatialQueries.TryFindDetour(state, unit, start, exactDestination, out var detour))
            {
                continuousDetour = detour;
            }
            else
            {
                path = unit.Position == goal ? null : FindPath(state, unitIndex, goal);
            }
        }
        if (path is null) return false;

        unit.Path.Clear();
        unit.Path.AddRange(path);
        unit.ContinuousWaypoints.Clear();
        unit.ContinuousWaypoints.AddRange(continuousDetour);
        unit.MovementDestinationMeters = exactDestination;
        unit.MovementProgress = 0;
        return true;
    }


    private static bool StraightLineTraversable(TacticalState state, TacticalUnit unit, Position3D from, Position3D to)
    {
        var distance = from.HorizontalDistanceTo(to);
        if (distance <= 0.01) return true;
        if (UrbanSpatialQueries.SegmentBlockedForMovement(state, from, to)) return false;
        var sampleSpacing = Math.Max(25.0, state.World.CellSizeMeters * 0.35);
        var samples = Math.Max(1, (int)Math.Ceiling(distance / sampleSpacing));
        for (var i = 0; i <= samples; i++)
        {
            var t = i / (double)samples;
            var x = from.X + (to.X - from.X) * t;
            var y = from.Y + (to.Y - from.Y) * t;
            var cell = state.World.GridPointFromWorld(x, y);
            if (!state.InBounds(cell.X, cell.Y) || !IsWalkable(state.Tiles[cell.X, cell.Y]) ||
                !state.Infrastructure.IsTraversable(cell, unit.UnitClass))
                return false;
        }
        return true;
    }

    public static void Step(TacticalState state)
    {
        if (state.Result != BattleResult.Ongoing) return;
        state.EnsurePrecisePositions();
        state.Tick++;
        state.Performance.BeginTick(state.Tick);
        state.OperationalSupport.Process(state.Tick);
        state.CommandAndControl.ProcessReports(state);
        TacticalAiEngine.Plan(state);

        foreach (var unit in state.Units.Where(unit => unit.Alive &&
                     (unit.Path.Count > 0 || unit.ContinuousWaypoints.Count > 0 || unit.MovementDestinationMeters is not null)))
        {
            AdvanceContinuousMovement(state, unit);
        }

        state.EnsurePrecisePositions();
        CohesionMoraleEngine.Update(state);
        DetectionEngine.Update(state);
        state.CommandAndControl.ProcessReports(state);
        EngagementEngine.Step(state);
        CasualtyLogisticsEngine.Update(state);
        TacticalAiEngine.UpdateBattleState(state);
    }

    private static void AdvanceContinuousMovement(TacticalState state, TacticalUnit unit)
    {
        var current = state.GroundPositionOf(unit);
        Position3D waypoint;

        if (unit.ContinuousWaypoints.Count > 0)
        {
            waypoint = unit.ContinuousWaypoints[0];
        }
        else if (unit.Path.Count > 0)
        {
            var nextCell = unit.Path[0];
            if (!state.Infrastructure.IsTraversable(nextCell, unit.UnitClass))
            {
                unit.Path.Clear();
                unit.ContinuousWaypoints.Clear();
                unit.MovementDestinationMeters = null;
                unit.CurrentSpeedMetersPerSecond = 0;
                unit.CurrentAccelerationMetersPerSecondSquared = 0;
                return;
            }
            if (HardOccupiedByOther(state, unit, nextCell)) return;
            waypoint = unit.Path.Count == 1 && unit.MovementDestinationMeters is { } exact
                ? exact
                : state.World.CellCenter(nextCell, 0);
        }
        else if (unit.MovementDestinationMeters is { } exact)
        {
            waypoint = exact;
        }
        else
        {
            return;
        }

        var dx = waypoint.X - current.X;
        var dy = waypoint.Y - current.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance > 0.05)
        {
            var heading = Math.Atan2(dx, dy) * 180.0 / Math.PI;
            if (heading < 0) heading += 360;
            unit.Orientation = unit.Orientation with { HeadingDegrees = heading };
        }
        if (distance <= 0.05)
        {
            unit.SetGroundPosition(state, waypoint);
            CompleteReachedWaypoint(state, unit, waypoint);
            return;
        }

        var terrain = state.World.ContextAt(current.X, current.Y).Terrain;
        var terrainMultiplier = terrain switch
        {
            TerrainType.Road => 1.0,
            TerrainType.Open => 0.92,
            TerrainType.Agricultural => 0.82,
            TerrainType.Scrub => 0.72,
            TerrainType.Rocky => 0.62,
            TerrainType.Forest => 0.58,
            TerrainType.UrbanSurface => 0.70,
            _ => 0.50
        };
        var fatigueMultiplier = unit.Soldier is null
            ? 1.0
            : Math.Clamp(1.0 - unit.Soldier.Vitals.Fatigue01 * 0.35 -
                         unit.Soldier.Vitals.Wounds.Sum(wound => wound.MobilityPenalty01) * 0.30, 0.30, 1.0);
        var suppressionMultiplier = unit.Soldier is null
            ? 1.0
            : Math.Clamp(1.0 - unit.Soldier.Vitals.Suppression01 * 0.28, 0.55, 1.0);
        var mobility = unit.Mobility;
        var desiredSpeed = mobility.MaxSpeedMetersPerSecond * terrainMultiplier *
                           state.Infrastructure.RoadMovementMultiplier(unit.Position) *
                           fatigueMultiplier * suppressionMultiplier;
        desiredSpeed = Math.Max(0, desiredSpeed);

        // Brake as the entity approaches the exact continuous destination. The planner may still use
        // a coarse internal cell graph, but physical motion never snaps to that graph.
        var brakingSpeed = Math.Sqrt(Math.Max(0, 2.0 * mobility.DecelerationMetersPerSecondSquared * distance));
        desiredSpeed = Math.Min(desiredSpeed, brakingSpeed);

        var dt = Math.Max(0.01, state.SecondsPerTick);
        var beforeSpeed = unit.CurrentSpeedMetersPerSecond;
        if (beforeSpeed < desiredSpeed)
            unit.CurrentSpeedMetersPerSecond = Math.Min(desiredSpeed, beforeSpeed + mobility.AccelerationMetersPerSecondSquared * dt);
        else
            unit.CurrentSpeedMetersPerSecond = Math.Max(desiredSpeed, beforeSpeed - mobility.DecelerationMetersPerSecondSquared * dt);
        unit.CurrentAccelerationMetersPerSecondSquared =
            (unit.CurrentSpeedMetersPerSecond - beforeSpeed) / dt;

        var maxStep = Math.Max(0.01, unit.CurrentSpeedMetersPerSecond * dt);
        if (maxStep >= distance)
        {
            unit.SetGroundPosition(state, waypoint);
            CompleteReachedWaypoint(state, unit, waypoint);
            return;
        }

        var fraction = maxStep / distance;
        var next = new Position3D(
            current.X + dx * fraction,
            current.Y + dy * fraction,
            current.Z);
        unit.SetGroundPosition(state, next);
    }

    private static void CompleteReachedWaypoint(TacticalState state, TacticalUnit unit, Position3D waypoint)
    {
        if (unit.ContinuousWaypoints.Count > 0 &&
            unit.ContinuousWaypoints[0].HorizontalDistanceTo(waypoint) <= 0.25)
        {
            unit.ContinuousWaypoints.RemoveAt(0);
        }

        if (unit.Path.Count > 0)
        {
            var reached = unit.Path[0];
            var reachedCenter = state.World.CellCenter(reached, 0);
            var sameCell = state.World.GridPointFromWorld(waypoint.X, waypoint.Y) == reached;
            if (sameCell || reachedCenter.HorizontalDistanceTo(waypoint) <= state.World.CellSizeMeters)
                unit.Path.RemoveAt(0);
        }

        if (unit.Path.Count == 0 && unit.ContinuousWaypoints.Count == 0 && unit.MovementDestinationMeters is { } exact &&
            state.GroundPositionOf(unit).HorizontalDistanceTo(exact) <= 0.10)
        {
            unit.SetGroundPosition(state, exact);
            unit.MovementDestinationMeters = null;
            unit.CurrentSpeedMetersPerSecond = 0;
            unit.CurrentAccelerationMetersPerSecondSquared = 0;
        }
    }

    public static bool HasLineOfSight(TacticalState state, GridPoint from, GridPoint to)
    {
        if (!state.InBounds(from.X, from.Y) || !state.InBounds(to.X, to.Y)) return false;
        return LineOfSightEngine.EvaluateCells(state, from, to).State != LineOfSightState.Blocked;
    }

    public static LineOfSightResult GetLineOfSight(
        TacticalState state, TacticalUnit observer, TacticalUnit target) =>
        LineOfSightEngine.Evaluate(state, observer, target);

    public static bool CellVisibleToFaction(TacticalState state, TacticalFaction faction, int x, int y)
    {
        if (!state.InBounds(x, y)) return false;
        var point = new GridPoint(x, y);
        return state.Units.Any(unit => unit.Alive && unit.Faction == faction &&
            Manhattan(unit.Position, point) <= unit.SightRange &&
            LineOfSightEngine.EvaluateCells(state, unit.Position, point, unit.EyeHeightMeters, 1.0)
                .State != LineOfSightState.Blocked);
    }

    public static bool CellVisibleToBlue(TacticalState state, int x, int y) =>
        CellVisibleToFaction(state, TacticalFaction.Blue, x, y);

    private static List<GridPoint>? FindPath(TacticalState state, int unitIndex, GridPoint goal)
    {
        state.Performance.RecordPathSearch();
        var unit = state.Units[unitIndex];
        if (!state.InBounds(goal.X, goal.Y) || !IsWalkable(state.Tiles[goal.X, goal.Y]) ||
            !state.Infrastructure.IsTraversable(goal, unit.UnitClass))
            return null;
        if (unit.UnitClass != TacticalUnitClass.Person && HardOccupiedByOther(state, unit, goal))
            return null;

        var count = state.Width * state.Height;
        var costs = Enumerable.Repeat(int.MaxValue, count).ToArray();
        var parents = Enumerable.Repeat(-1, count).ToArray();
        var closed = new bool[count];
        var start = Index(state, unit.Position.X, unit.Position.Y);
        var target = Index(state, goal.X, goal.Y);
        var open = new PriorityQueue<int, int>();

        costs[start] = 0;
        open.Enqueue(start, Heuristic(unit.Position, goal));

        while (open.Count > 0)
        {
            var current = open.Dequeue();
            if (closed[current]) continue;
            if (current == target) break;
            closed[current] = true;
            state.Performance.RecordPathExpansion();
            var point = Point(state, current);

            foreach (var direction in Directions)
            {
                var x = point.X + direction.X;
                var y = point.Y + direction.Y;
                if (!state.InBounds(x, y) || !IsWalkable(state.Tiles[x, y])) continue;
                var candidate = new GridPoint(x, y);
                if (!state.Infrastructure.IsTraversable(candidate, unit.UnitClass)) continue;
                var diagonal = direction.X != 0 && direction.Y != 0;
                if (diagonal)
                {
                    var sideA = new GridPoint(point.X + direction.X, point.Y);
                    var sideB = new GridPoint(point.X, point.Y + direction.Y);
                    if (!IsWalkable(state.Tiles[sideA.X, sideA.Y]) || !IsWalkable(state.Tiles[sideB.X, sideB.Y]))
                        continue;
                }
                if (unit.UnitClass != TacticalUnitClass.Person && candidate != goal &&
                    HardOccupiedByOther(state, unit, candidate)) continue;

                var neighbor = Index(state, x, y);
                if (closed[neighbor]) continue;
                var stepCost = MoveCost(state.Tiles[x, y]) + (diagonal ? 2 : 0);
                var tentative = costs[current] + stepCost;
                if (tentative >= costs[neighbor]) continue;

                parents[neighbor] = current;
                costs[neighbor] = tentative;
                open.Enqueue(neighbor, tentative + Heuristic(candidate, goal));
            }
        }

        if (target != start && parents[target] < 0) return null;
        var reverse = new List<GridPoint>();
        for (var current = target; current != start; current = parents[current])
        {
            if (current < 0) return null;
            reverse.Add(Point(state, current));
        }
        reverse.Reverse();
        return reverse;
    }

    private static bool HardOccupiedByOther(TacticalState state, TacticalUnit movingUnit, GridPoint point)
    {
        if (movingUnit.UnitClass == TacticalUnitClass.Person) return false;
        EnsureIndexes(state);
        var center = state.World.CellCenter(point, 0);
        var radius = state.World.CellSizeMeters * 0.72;
        var nearby = state.SpatialIndex.QueryRadius(center, radius);
        state.Performance.RecordSpatialQuery(nearby.Count);
        foreach (var id in nearby)
        {
            var other = state.UnitById(id);
            if (other is null || !other.Alive || other.Id == movingUnit.Id || other.Position != point) continue;
            if (other.UnitClass != TacticalUnitClass.Person) return true;
        }
        return false;
    }

    private static void EnsureIndexes(TacticalState state) => state.EnsurePrecisePositions();

    private static int Index(TacticalState state, int x, int y) => y * state.Width + x;
    private static GridPoint Point(TacticalState state, int index) =>
        new(index % state.Width, index / state.Width);
    private static int Manhattan(GridPoint first, GridPoint second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
    private static int Heuristic(GridPoint first, GridPoint second) =>
        (int)Math.Round(Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2)) * 4);
}
