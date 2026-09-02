namespace FieldSim.Core;

public enum SupportAssetKind
{
    TubeArtillery,
    RocketArtillery,
    AttackHelicopter,
    RescueHelicopter,
    TacticalUas,
    MaleUas,
    FixedWingStrike,
    MedicalTeam,
    EngineerRepairTeam,
    CounterBatteryRadar
}

public enum SupportRequestKind
{
    Observation,
    FireSupport,
    AirSupport,
    CasualtyEvacuation,
    RouteRepair,
    BridgeRepair,
    CounterBatteryCue,
    StrikeAssessment
}

public enum SupportRequestStatus
{
    Draft,
    Transmitting,
    Acknowledged,
    Assigned,
    Executing,
    Completed,
    Rejected,
    Cancelled
}

public sealed class SupportAssetDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required SupportAssetKind Kind { get; init; }
    public string? PublicPlatformId { get; init; }
    public int? PublicEnduranceHours { get; init; }
    public int? PublicCeilingFeet { get; init; }
    public required string DataBoundary { get; init; }
}

public sealed class SupportAssetCatalog
{
    public required string Mode { get; init; }
    public required string DataBoundary { get; init; }
    public required List<SupportAssetCatalogRecord> Assets { get; init; }
}

public sealed class SupportAssetCatalogRecord
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required SupportAssetKind Kind { get; init; }
    public string? PublicPlatformId { get; init; }
    public int? ScenarioQuantity { get; init; }
    public string? OperatingZoneId { get; init; }
    public required string DataBoundary { get; init; }
}

public sealed class SupportAssetRuntime
{
    public required string Id { get; init; }
    public required SupportAssetDefinition Definition { get; init; }
    public required AssetState Availability { get; init; }
    // Null means a deliberately faction-agnostic test/scaffold asset. Runtime scenario assets
    // should set faction so requests cannot cross-assign between opposing sides.
    public TacticalFaction? Faction { get; set; }
    public string OperatingZoneId { get; set; } = "unassigned";
    public string? CurrentRequestId { get; set; }
}

public sealed class SupportRequest
{
    public required string Id { get; init; }
    public required string RequesterEntityKey { get; init; }
    public required TacticalFaction Faction { get; init; }
    public required SupportRequestKind Kind { get; init; }
    public GridPoint? ObjectiveCell { get; init; }
    public Position3D? ObjectivePositionMeters { get; init; }
    public int Priority { get; init; } = 50;
    public long CreatedTick { get; init; }
    public long EarliestExecutionTick { get; set; }
    public long? AssignedTick { get; set; }
    public long? ExecuteAtTick { get; set; }
    public long? CompleteAtTick { get; set; }
    public SupportRequestStatus Status { get; set; } = SupportRequestStatus.Draft;
    public string? AssignedAssetId { get; set; }
    public string StatusReason { get; set; } = "Awaiting transmission";
}

public sealed class OperationalSupportState
{
    public List<SupportAssetRuntime> Assets { get; } = [];
    public List<SupportRequest> Requests { get; } = [];

    public IReadOnlyList<SupportAssetRuntime> ReadyAssets(TacticalFaction faction, SupportAssetKind kind) => Assets
        .Where(asset => asset.Faction == faction && asset.Definition.Kind == kind &&
                        asset.Availability.Status == AssetStatus.Ready)
        .ToArray();

    public bool Queue(SupportRequest request, int transmissionDelayTicks)
    {
        if (Requests.Any(item => string.Equals(item.Id, request.Id, StringComparison.Ordinal))) return false;
        request.Status = SupportRequestStatus.Transmitting;
        request.EarliestExecutionTick = request.CreatedTick + Math.Max(1, transmissionDelayTicks);
        request.StatusReason = "Moving through the simulated command network";
        Requests.Add(request);
        return true;
    }

    // Assignment is deliberately generic. Effects, route selection, pickup zones and target
    // approval belong to later scenario/rules modules rather than being guessed here.
    public void Process(long tick)
    {
        foreach (var request in Requests
                     .Where(item => item.Status == SupportRequestStatus.Transmitting && item.EarliestExecutionTick <= tick)
                     .OrderByDescending(item => item.Priority)
                     .ThenBy(item => item.CreatedTick))
        {
            request.Status = SupportRequestStatus.Acknowledged;
            request.StatusReason = "Acknowledged; awaiting a compatible available asset";
        }

        // Acknowledged requests remain eligible on later ticks. This prevents a request from
        // becoming permanently stuck just because every compatible asset was busy at first ACK.
        foreach (var request in Requests
                     .Where(item => item.Status == SupportRequestStatus.Acknowledged)
                     .OrderByDescending(item => item.Priority)
                     .ThenBy(item => item.CreatedTick))
        {
            var compatible = Assets.FirstOrDefault(asset =>
                asset.Availability.Status == AssetStatus.Ready &&
                (asset.Faction is null || asset.Faction == request.Faction) &&
                Compatible(asset.Definition.Kind, request.Kind));
            if (compatible is null) continue;
            if (!compatible.Availability.Assign()) continue;
            compatible.CurrentRequestId = request.Id;
            request.AssignedAssetId = compatible.Id;
            request.AssignedTick = tick;
            request.ExecuteAtTick = tick + 3;
            request.CompleteAtTick = tick + 12;
            request.Status = SupportRequestStatus.Assigned;
            request.StatusReason = "Compatible same-side asset assigned; abstract execution is queued";
        }

        foreach (var request in Requests.Where(item => item.Status == SupportRequestStatus.Assigned &&
                     item.ExecuteAtTick is not null && item.ExecuteAtTick <= tick))
        {
            request.Status = SupportRequestStatus.Executing;
            request.StatusReason = "Assigned support is executing the abstract task";
        }

        foreach (var request in Requests.Where(item => item.Status == SupportRequestStatus.Executing &&
                     item.CompleteAtTick is not null && item.CompleteAtTick <= tick))
        {
            request.Status = SupportRequestStatus.Completed;
            request.StatusReason = "Abstract support task completed";
            var asset = Assets.FirstOrDefault(item => string.Equals(item.Id, request.AssignedAssetId, StringComparison.Ordinal));
            if (asset is null) continue;
            asset.CurrentRequestId = null;
            asset.Availability.ReleaseToReady();
        }

        if (Requests.Count > 800)
            Requests.RemoveAll(item =>
                (item.Status is SupportRequestStatus.Completed or SupportRequestStatus.Rejected or SupportRequestStatus.Cancelled) &&
                tick - item.CreatedTick > 600);
    }

    private static bool Compatible(SupportAssetKind asset, SupportRequestKind request) => request switch
    {
        SupportRequestKind.Observation => asset is SupportAssetKind.TacticalUas or SupportAssetKind.MaleUas,
        SupportRequestKind.FireSupport => asset is SupportAssetKind.TubeArtillery or SupportAssetKind.RocketArtillery,
        SupportRequestKind.AirSupport => asset is SupportAssetKind.AttackHelicopter or SupportAssetKind.FixedWingStrike,
        SupportRequestKind.CasualtyEvacuation => asset is SupportAssetKind.RescueHelicopter or SupportAssetKind.MedicalTeam,
        SupportRequestKind.RouteRepair or SupportRequestKind.BridgeRepair => asset == SupportAssetKind.EngineerRepairTeam,
        _ => false
    };
}
