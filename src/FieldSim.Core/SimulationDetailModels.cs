namespace FieldSim.Core;

/// <summary>
/// Multi-resolution simulation levels. The level changes how much work is performed, not the
/// identity of the formation or entity. Continuous world coordinates remain valid at every level.
/// </summary>
public enum SimulationDetailLevel
{
    AggregateFormation,
    Formation,
    Entity,
    CloseQuarters
}

public sealed record SimulationLodPolicy(
    double EntityRadiusMeters = 2500,
    double CloseQuartersRadiusMeters = 300,
    long MinimumResidenceTicks = 10)
{
    public SimulationDetailLevel Select(
        double distanceFromInterestMeters,
        bool directlySelected,
        bool activeCombat,
        bool insideDetailedStructure)
    {
        if (insideDetailedStructure || directlySelected && distanceFromInterestMeters <= CloseQuartersRadiusMeters)
            return SimulationDetailLevel.CloseQuarters;
        if (directlySelected || activeCombat || distanceFromInterestMeters <= EntityRadiusMeters)
            return SimulationDetailLevel.Entity;
        return distanceFromInterestMeters <= EntityRadiusMeters * 4
            ? SimulationDetailLevel.Formation
            : SimulationDetailLevel.AggregateFormation;
    }
}

public sealed class SimulationLodAssignment
{
    public required string StableKey { get; init; }
    public SimulationDetailLevel Level { get; set; } = SimulationDetailLevel.Entity;
    public long LastChangedTick { get; set; }
}

/// <summary>
/// Stores level-of-detail decisions separately from entity state. v1.7 introduces the contract;
/// later versions can plug aggregate/expand serializers into it without rewriting the world model.
/// </summary>
public sealed class SimulationLodRegistry
{
    private readonly Dictionary<string, SimulationLodAssignment> _assignments = new(StringComparer.Ordinal);

    public IReadOnlyCollection<SimulationLodAssignment> Assignments => _assignments.Values;

    public SimulationDetailLevel Get(string stableKey) =>
        _assignments.TryGetValue(stableKey, out var assignment)
            ? assignment.Level
            : SimulationDetailLevel.Entity;

    public bool TrySet(string stableKey, SimulationDetailLevel level, long tick, long minimumResidenceTicks = 0)
    {
        if (!_assignments.TryGetValue(stableKey, out var assignment))
        {
            _assignments[stableKey] = new SimulationLodAssignment
            {
                StableKey = stableKey,
                Level = level,
                LastChangedTick = tick
            };
            return true;
        }

        if (assignment.Level == level) return false;
        if (tick - assignment.LastChangedTick < Math.Max(0, minimumResidenceTicks)) return false;
        assignment.Level = level;
        assignment.LastChangedTick = tick;
        return true;
    }
}
