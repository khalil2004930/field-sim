namespace FieldSim.Core;

/// <summary>
/// Converts one high-level destination into deterministic nearby entity destinations. The ORBAT
/// node itself never moves; only member entities receive destinations. The spread is a synthetic
/// simulation convenience to avoid every entity collapsing onto the same coordinate.
/// </summary>
public static class FormationDestinationAllocator
{
    private const double GoldenAngleRadians = 2.399963229728653;

    public static IReadOnlyDictionary<int, Position3D> Allocate(
        TacticalState state,
        IEnumerable<TacticalUnit> units,
        Position3D centerMeters)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(units);

        var ordered = units
            .Where(unit => unit.Alive)
            .OrderBy(unit => unit.EntityKey, StringComparer.Ordinal)
            .ThenBy(unit => unit.Id)
            .ToArray();
        var result = new Dictionary<int, Position3D>();
        if (ordered.Length == 0) return result;

        var worldMaxX = state.Width * state.World.CellSizeMeters - 0.01;
        var worldMaxY = state.Height * state.World.CellSizeMeters - 0.01;
        for (var index = 0; index < ordered.Length; index++)
        {
            var unit = ordered[index];
            var baseSpacing = unit.UnitClass == TacticalUnitClass.Person ? 2.75 : 8.0;
            var ring = Math.Sqrt(index);
            var radius = baseSpacing * ring;
            var angle = index * GoldenAngleRadians;
            var x = Math.Clamp(centerMeters.X + Math.Cos(angle) * radius, 0.01, worldMaxX);
            var y = Math.Clamp(centerMeters.Y + Math.Sin(angle) * radius, 0.01, worldMaxY);
            var z = state.World.GroundAltitudeAt(x, y);
            result[unit.Id] = new Position3D(x, y, z);
        }
        return result;
    }
}
