namespace FieldSim.Core;

/// <summary>
/// Cheap per-tick counters used to find scaling regressions before they become frame-time
/// problems. They are diagnostic values only and never influence simulation outcomes.
/// </summary>
public sealed class SimulationPerformanceCounters
{
    public long Tick { get; private set; }
    public int SpatialRadiusQueries { get; private set; }
    public int SpatialCandidatesExamined { get; private set; }
    public int LineOfSightEvaluations { get; private set; }
    public int DetectionObserverScans { get; private set; }
    public int DetectionCandidatePairs { get; private set; }
    public int PathSearches { get; private set; }
    public int PathNodesExpanded { get; private set; }

    public void BeginTick(long tick)
    {
        Tick = tick;
        SpatialRadiusQueries = 0;
        SpatialCandidatesExamined = 0;
        LineOfSightEvaluations = 0;
        DetectionObserverScans = 0;
        DetectionCandidatePairs = 0;
        PathSearches = 0;
        PathNodesExpanded = 0;
    }

    public void RecordSpatialQuery(int candidateCount)
    {
        SpatialRadiusQueries++;
        SpatialCandidatesExamined += Math.Max(0, candidateCount);
    }

    public void RecordLineOfSight() => LineOfSightEvaluations++;
    public void RecordDetectionObserver() => DetectionObserverScans++;
    public void RecordDetectionCandidates(int count) => DetectionCandidatePairs += Math.Max(0, count);
    public void RecordPathSearch() => PathSearches++;
    public void RecordPathExpansion() => PathNodesExpanded++;

    public SimulationPerformanceSnapshot Snapshot() => new(
        Tick,
        SpatialRadiusQueries,
        SpatialCandidatesExamined,
        LineOfSightEvaluations,
        DetectionObserverScans,
        DetectionCandidatePairs,
        PathSearches,
        PathNodesExpanded);
}

public sealed record SimulationPerformanceSnapshot(
    long Tick,
    int SpatialRadiusQueries,
    int SpatialCandidatesExamined,
    int LineOfSightEvaluations,
    int DetectionObserverScans,
    int DetectionCandidatePairs,
    int PathSearches,
    int PathNodesExpanded);
