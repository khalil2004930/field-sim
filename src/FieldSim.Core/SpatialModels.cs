namespace FieldSim.Core;

public enum TerrainType
{
    Open,
    Road,
    Forest,
    Scrub,
    Agricultural,
    Rocky,
    Mountain,
    Water,
    UrbanSurface
}

public enum AreaType
{
    Wilderness,
    OpenTerrain,
    Forest,
    Village,
    Town,
    City,
    Industrial,
    Water
}

public enum TerritoryState
{
    Friendly,
    Hostile,
    Contested,
    Neutral,
    Battlefield
}

public enum LineOfSightState
{
    Clear,
    Obscured,
    Blocked
}

public enum SensorType
{
    Visual,
    Thermal,
    Radar,
    Acoustic
}

public enum ContactClassification
{
    Unknown,
    Contact,
    Person,
    Vehicle,
    ArmoredVehicle,
    AirObject,
    Identified
}

public readonly record struct Position3D(double X, double Y, double Z)
{
    public double HorizontalDistanceTo(Position3D other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    public double DistanceTo(Position3D other)
    {
        var dx = other.X - X;
        var dy = other.Y - Y;
        var dz = other.Z - Z;
        return Math.Sqrt(dx * dx + dy * dy + dz * dz);
    }
}

public readonly record struct Orientation3D(double HeadingDegrees, double PitchDegrees, double RollDegrees)
{
    public static Orientation3D Zero => new(0, 0, 0);
}

public readonly record struct Bounds3D(double LengthMeters, double WidthMeters, double HeightMeters)
{
    public static Bounds3D Human => new(0.55, 0.45, 1.8);
}

public sealed record SpatialContext(
    TerrainType Terrain,
    AreaType Area,
    TerritoryState Territory,
    double GroundAltitudeMeters,
    double VegetationDensity,
    double BuildingDensity,
    double Cover,
    double Concealment,
    double ObstacleHeightMeters,
    TacticalFaction? ControllingFaction = null)
{
    public SpatialContext WithGroundAltitude(double altitudeMeters) => this with
    {
        GroundAltitudeMeters = altitudeMeters
    };
}

public sealed record SignatureProfile(
    double Visual,
    double Thermal,
    double Radar,
    double Acoustic)
{
    public static SignatureProfile Human => new(0.35, 0.45, 0.08, 0.12);

    public double For(SensorType sensorType) => sensorType switch
    {
        SensorType.Visual => Visual,
        SensorType.Thermal => Thermal,
        SensorType.Radar => Radar,
        SensorType.Acoustic => Acoustic,
        _ => 0
    };

    public SignatureProfile Clamp() => new(
        Math.Clamp(Visual, 0, 1),
        Math.Clamp(Thermal, 0, 1),
        Math.Clamp(Radar, 0, 1),
        Math.Clamp(Acoustic, 0, 1));
}

public sealed record SensorDefinition(
    string Id,
    string Name,
    SensorType Type,
    double MountHeightMeters,
    double MaximumRangeMeters,
    double DetectionStrength,
    double IdentificationStrength,
    double FieldOfViewDegrees = 360)
{
    public SensorDefinition Clamp() => this with
    {
        MountHeightMeters = Math.Max(0, MountHeightMeters),
        MaximumRangeMeters = Math.Max(1, MaximumRangeMeters),
        DetectionStrength = Math.Clamp(DetectionStrength, 0, 1),
        IdentificationStrength = Math.Clamp(IdentificationStrength, 0, 1),
        FieldOfViewDegrees = Math.Clamp(FieldOfViewDegrees, 1, 360)
    };
}

public sealed record LineOfSightResult(
    LineOfSightState State,
    double HorizontalDistanceMeters,
    double MinimumClearanceMeters,
    Position3D? FirstBlockingPoint,
    double ObscurationFactor,
    string Reason);

public sealed class DetectionContact
{
    public required int ObserverUnitId { get; set; }
    public required int TargetUnitId { get; set; }
    public required TacticalFaction ObserverFaction { get; set; }
    public required Position3D LastKnownPosition { get; set; }
    public required long LastDetectedTick { get; set; }
    public required ContactClassification Classification { get; set; }
    public double DetectionConfidence { get; set; }
    public double IdentificationConfidence { get; set; }
}

public sealed class FactionKnowledge
{
    private readonly Dictionary<int, DetectionContact> _contacts = [];

    public FactionKnowledge(TacticalFaction faction) => Faction = faction;

    public TacticalFaction Faction { get; }
    public IReadOnlyCollection<DetectionContact> Contacts => _contacts.Values;

    public DetectionContact? GetContact(int targetUnitId) =>
        _contacts.GetValueOrDefault(targetUnitId);

    public bool Knows(int targetUnitId, long currentTick, long memoryTicks = 80) =>
        _contacts.TryGetValue(targetUnitId, out var contact) &&
        currentTick - contact.LastDetectedTick <= memoryTicks;

    public void UpdateContact(DetectionContact contact) =>
        _contacts[contact.TargetUnitId] = contact;

    public void Clear() => _contacts.Clear();
}
