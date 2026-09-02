namespace FieldSim.Core;

public enum StructureVolumeType
{
    Building,
    Bunker,
    Tunnel,
    Trench,
    Cave,
    Utility,
    Other
}

public enum StructureMaterialClass
{
    LightPartition,
    Masonry,
    ReinforcedConcrete,
    Earth,
    Rock,
    Metal,
    Mixed,
    Unknown
}

public enum NavigationPortalType
{
    Door,
    Opening,
    Stair,
    Ladder,
    Ramp,
    Hatch,
    TunnelConnection
}

public readonly record struct AxisAlignedBounds3D(Position3D Minimum, Position3D Maximum)
{
    public bool Contains(Position3D point) =>
        point.X >= Minimum.X && point.X <= Maximum.X &&
        point.Y >= Minimum.Y && point.Y <= Maximum.Y &&
        point.Z >= Minimum.Z && point.Z <= Maximum.Z;

    public Position3D Center => new(
        (Minimum.X + Maximum.X) * 0.5,
        (Minimum.Y + Maximum.Y) * 0.5,
        (Minimum.Z + Maximum.Z) * 0.5);
}

public sealed class StructureCompartment
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public int LevelIndex { get; init; }
    public required AxisAlignedBounds3D Bounds { get; init; }
    public StructureMaterialClass DominantMaterial { get; init; } = StructureMaterialClass.Unknown;
    public bool Navigable { get; init; } = true;
}

public sealed record NavigationPortal(
    string Id,
    string FromCompartmentId,
    string ToCompartmentId,
    NavigationPortalType Type,
    Position3D PositionMeters,
    bool InitiallyPassable = true);

public sealed class StructureVolume
{
    private Dictionary<string, StructureCompartment>? _compartmentsById;

    public required string Id { get; init; }
    public required string Name { get; init; }
    public StructureVolumeType Type { get; init; } = StructureVolumeType.Building;
    public required AxisAlignedBounds3D Bounds { get; init; }
    public List<StructureCompartment> Compartments { get; init; } = [];
    public List<NavigationPortal> Portals { get; init; } = [];
    public bool SyntheticScenarioElement { get; init; } = true;

    public StructureCompartment? CompartmentAt(Position3D position)
    {
        if (!Bounds.Contains(position)) return null;
        return Compartments.FirstOrDefault(compartment => compartment.Bounds.Contains(position));
    }

    public StructureCompartment? FindCompartment(string id)
    {
        _compartmentsById ??= Compartments.ToDictionary(item => item.Id, StringComparer.Ordinal);
        return _compartmentsById.GetValueOrDefault(id);
    }

    public IEnumerable<NavigationPortal> PortalsFor(string compartmentId) =>
        Portals.Where(portal =>
            string.Equals(portal.FromCompartmentId, compartmentId, StringComparison.Ordinal) ||
            string.Equals(portal.ToCompartmentId, compartmentId, StringComparison.Ordinal));
}
