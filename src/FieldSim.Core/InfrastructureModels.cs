namespace FieldSim.Core;

public enum InfrastructureDamageState { Intact, Light, Moderate, Severe, Destroyed }
public enum RoadSurfaceClass { Paved, Unpaved, Track, BridgeDeck }

public sealed class BuildingState
{
    public required string Id { get; init; }
    public string Name { get; init; } = "Building";
    public required IReadOnlyList<GridPoint> FootprintCells { get; init; }
    public bool HasBasement { get; init; }
    public bool HasBunker { get; init; }
    public bool HasTunnelEntrance { get; init; }
    public double IntegrityPercent { get; private set; } = 100;
    public InfrastructureDamageState DamageState => InfrastructureNetwork.ClassifyDamage(IntegrityPercent);
    public bool Habitable => IntegrityPercent >= 35;
    public bool PassableOnFoot => IntegrityPercent < 15;

    public void ApplyDamage(double damagePoints) =>
        IntegrityPercent = Math.Clamp(IntegrityPercent - Math.Max(0, damagePoints), 0, 100);

    public void Repair(double repairPoints, double maximumRestorablePercent = 85) =>
        IntegrityPercent = Math.Min(Math.Clamp(maximumRestorablePercent, 0, 100),
            IntegrityPercent + Math.Max(0, repairPoints));
}

public sealed class RoadSegmentState
{
    public required string Id { get; init; }
    public string Name { get; init; } = "Road segment";
    public required IReadOnlyList<GridPoint> Cells { get; init; }
    public RoadSurfaceClass Surface { get; init; } = RoadSurfaceClass.Paved;
    public bool IsBridge { get; init; }
    public double IntegrityPercent { get; private set; } = 100;
    public double ObstructionPercent { get; private set; }
    public InfrastructureDamageState DamageState => InfrastructureNetwork.ClassifyDamage(IntegrityPercent);
    public bool UsableByVehicles => IntegrityPercent >= (IsBridge ? 45 : 20) && ObstructionPercent < 75;
    public bool UsableOnFoot => IntegrityPercent >= (IsBridge ? 20 : 5) && ObstructionPercent < 95;
    public double CapacityMultiplier => !UsableOnFoot ? 0 : Math.Clamp(
        IntegrityPercent / 100.0 * (1 - ObstructionPercent / 120.0), 0.05, 1);

    public void ApplyDamage(double damagePoints, double obstructionPoints = 0)
    {
        IntegrityPercent = Math.Clamp(IntegrityPercent - Math.Max(0, damagePoints), 0, 100);
        ObstructionPercent = Math.Clamp(ObstructionPercent + Math.Max(0, obstructionPoints), 0, 100);
    }

    public void Repair(double repairPoints, double clearancePoints)
    {
        IntegrityPercent = Math.Clamp(IntegrityPercent + Math.Max(0, repairPoints), 0, 100);
        ObstructionPercent = Math.Clamp(ObstructionPercent - Math.Max(0, clearancePoints), 0, 100);
    }
}

public sealed class InfrastructureNetwork
{
    private readonly Dictionary<GridPoint, BuildingState> _buildingByCell = [];
    private readonly Dictionary<GridPoint, RoadSegmentState> _roadByCell = [];
    public List<BuildingState> Buildings { get; } = [];
    public List<RoadSegmentState> Roads { get; } = [];

    public void SeedFromTiles(TacticalTile[,] tiles)
    {
        Buildings.Clear(); Roads.Clear(); _buildingByCell.Clear(); _roadByCell.Clear();
        for (var y = 0; y < tiles.GetLength(1); y++)
        for (var x = 0; x < tiles.GetLength(0); x++)
        {
            var point = new GridPoint(x, y);
            if (tiles[x, y] == TacticalTile.Building)
            {
                var building = new BuildingState
                {
                    Id = $"building-{x:D2}-{y:D2}", Name = $"Synthetic building {x:D2}-{y:D2}",
                    FootprintCells = [point]
                };
                Buildings.Add(building); _buildingByCell[point] = building;
            }
            else if (tiles[x, y] == TacticalTile.Road)
            {
                var road = new RoadSegmentState
                {
                    Id = $"road-{x:D2}-{y:D2}", Name = $"Road cell {x:D2}-{y:D2}", Cells = [point]
                };
                Roads.Add(road); _roadByCell[point] = road;
            }
        }
    }

    public BuildingState? BuildingAt(GridPoint point) => _buildingByCell.GetValueOrDefault(point);
    public RoadSegmentState? RoadAt(GridPoint point) => _roadByCell.GetValueOrDefault(point);
    public bool IsTraversable(GridPoint point, TacticalUnitClass unitClass)
    {
        if (unitClass == TacticalUnitClass.AirObject) return true;
        var road = RoadAt(point);
        return road is null || (unitClass == TacticalUnitClass.Person ? road.UsableOnFoot : road.UsableByVehicles);
    }
    public double RoadMovementMultiplier(GridPoint point) => RoadAt(point)?.CapacityMultiplier ?? 1;
    public static InfrastructureDamageState ClassifyDamage(double integrityPercent) => integrityPercent switch
    {
        >= 90 => InfrastructureDamageState.Intact, >= 70 => InfrastructureDamageState.Light,
        >= 45 => InfrastructureDamageState.Moderate, >= 15 => InfrastructureDamageState.Severe,
        _ => InfrastructureDamageState.Destroyed
    };
}
