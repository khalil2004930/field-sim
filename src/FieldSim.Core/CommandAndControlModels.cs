namespace FieldSim.Core;

public enum ContactConfidenceState
{
    Suspected,
    Probable,
    Confirmed,
    Stale
}

public enum ContactReportStatus
{
    Queued,
    Delivered,
    Dropped
}

public sealed class ContactReport
{
    public required string Id { get; init; }
    public required int ObserverUnitId { get; init; }
    public required int TargetUnitId { get; init; }
    public required TacticalFaction Faction { get; init; }
    public required Position3D LastKnownPosition { get; init; }
    public required ContactClassification Classification { get; init; }
    public required double DetectionConfidence { get; init; }
    public required double IdentificationConfidence { get; init; }
    public required long ObservedTick { get; init; }
    public required long CreatedTick { get; init; }
    public required long DeliverAtTick { get; init; }
    public required string Channel { get; init; }
    public ContactReportStatus Status { get; set; } = ContactReportStatus.Queued;
    public long? DeliveredTick { get; set; }
    public string StatusReason { get; set; } = "Queued for simulated command-network delivery";
}

/// <summary>
/// First-pass C2 model. Sensors update the observing entity's local knowledge immediately.
/// Contact reports then move into faction-level knowledge after a deterministic abstract delay.
/// The coefficients are game abstractions, not real communications performance figures.
/// </summary>
public sealed class CommandAndControlState
{
    private readonly Dictionary<int, FactionKnowledge> _localKnowledge = [];
    private readonly Dictionary<(int ObserverId, int TargetId), long> _lastReportTick = [];
    private long _nextReportId = 1;

    public IReadOnlyDictionary<int, FactionKnowledge> LocalKnowledge => _localKnowledge;
    public List<ContactReport> ContactReports { get; } = [];

    public FactionKnowledge LocalFor(TacticalUnit unit)
    {
        if (!_localKnowledge.TryGetValue(unit.Id, out var knowledge))
        {
            knowledge = new FactionKnowledge(unit.Faction);
            _localKnowledge[unit.Id] = knowledge;
        }
        return knowledge;
    }

    public IReadOnlyList<DetectionContact> ContactsKnownBy(
        TacticalState state,
        TacticalUnit unit,
        long memoryTicks = 40)
    {
        var local = LocalFor(unit).Contacts
            .Where(contact => state.Tick - contact.LastDetectedTick <= memoryTicks);
        var shared = state.Knowledge[unit.Faction].Contacts
            .Where(contact => state.Tick - contact.LastDetectedTick <= memoryTicks);

        return local.Concat(shared)
            .GroupBy(contact => contact.TargetUnitId)
            .Select(group => group
                .OrderByDescending(contact => contact.LastDetectedTick)
                .ThenByDescending(contact => contact.IdentificationConfidence)
                .First())
            .ToArray();
    }

    public bool Knows(TacticalState state, TacticalUnit unit, int targetUnitId, long memoryTicks = 40)
    {
        var local = LocalFor(unit).GetContact(targetUnitId);
        if (local is not null && state.Tick - local.LastDetectedTick <= memoryTicks) return true;
        return state.Knowledge[unit.Faction].Knows(targetUnitId, state.Tick, memoryTicks);
    }

    public bool HasRecentLocalContact(long currentTick, long memoryTicks = 3) =>
        _localKnowledge.Values.SelectMany(item => item.Contacts)
            .Any(contact => currentTick - contact.LastDetectedTick <= memoryTicks);

    public ContactConfidenceState ConfidenceState(DetectionContact contact, long currentTick)
    {
        var age = Math.Max(0, currentTick - contact.LastDetectedTick);
        if (age > 45) return ContactConfidenceState.Stale;
        if (contact.IdentificationConfidence >= 0.68 && contact.DetectionConfidence >= 0.68)
            return ContactConfidenceState.Confirmed;
        if (contact.DetectionConfidence >= 0.48 || contact.IdentificationConfidence >= 0.46)
            return ContactConfidenceState.Probable;
        return ContactConfidenceState.Suspected;
    }

    public void RegisterDetection(
        TacticalState state,
        TacticalUnit observer,
        TacticalUnit target,
        Position3D lastKnownPosition,
        ContactClassification classification,
        double detectionConfidence,
        double identificationConfidence)
    {
        var local = LocalFor(observer);
        var existing = local.GetContact(target.Id);
        var isNew = existing is null;
        var classificationImproved = existing is not null && classification > existing.Classification;
        var confidenceImproved = existing is not null &&
            (detectionConfidence > existing.DetectionConfidence + 0.12 ||
             identificationConfidence > existing.IdentificationConfidence + 0.12);

        if (existing is null)
        {
            local.UpdateContact(new DetectionContact
            {
                ObserverUnitId = observer.Id,
                TargetUnitId = target.Id,
                ObserverFaction = observer.Faction,
                LastKnownPosition = lastKnownPosition,
                LastDetectedTick = state.Tick,
                Classification = classification,
                DetectionConfidence = detectionConfidence,
                IdentificationConfidence = identificationConfidence
            });
        }
        else
        {
            existing.ObserverUnitId = observer.Id;
            existing.LastKnownPosition = lastKnownPosition;
            existing.LastDetectedTick = state.Tick;
            existing.DetectionConfidence = Math.Max(existing.DetectionConfidence * 0.75, detectionConfidence);
            existing.IdentificationConfidence = Math.Max(existing.IdentificationConfidence * 0.75, identificationConfidence);
            existing.Classification = (ContactClassification)Math.Max((int)existing.Classification, (int)classification);
        }

        if (isNew)
        {
            state.AddActivity(TacticalEventType.Contact,
                $"LOCAL CONTACT: {observer.DisplayName} detected {target.DisplayName} as {classification}; report not yet shared.",
                observer.Faction, observer.Id);
        }

        var key = (observer.Id, target.Id);
        var lastReport = _lastReportTick.GetValueOrDefault(key, -1_000_000);
        if (!isNew && !classificationImproved && !confidenceImproved && state.Tick - lastReport < 6) return;
        _lastReportTick[key] = state.Tick;
        QueueReport(state, observer, target, lastKnownPosition, classification,
            detectionConfidence, identificationConfidence);
    }

    private void QueueReport(
        TacticalState state,
        TacticalUnit observer,
        TacticalUnit target,
        Position3D lastKnownPosition,
        ContactClassification classification,
        double detectionConfidence,
        double identificationConfidence)
    {
        var soldier = observer.Soldier;
        var hasRadio = soldier?.Equipment.Radio == true;
        var leadership = soldier?.Role is SoldierRole.Leader or SoldierRole.TeamLeader or SoldierRole.RadioOperator;
        var baseDelay = hasRadio ? leadership ? 1 : 2 : 6;
        var suppressionPenalty = (int)Math.Ceiling((soldier?.Vitals.Suppression01 ?? 0) * (hasRadio ? 3 : 6));
        var jitter = state.Random.NextInclusive(0, hasRadio ? 2 : 4);
        var delay = Math.Clamp(baseDelay + suppressionPenalty + jitter, 1, 18);
        var channel = hasRadio ? "abstract radio net" : "local relay / voice chain";

        // Small game-level message-loss chance makes communications imperfect without encoding
        // real-world frequencies, waveforms, procedures, or measured reliability.
        var lossChance = hasRadio ? 0.015 : 0.08;
        lossChance += (soldier?.Vitals.Suppression01 ?? 0) * (hasRadio ? 0.03 : 0.10);
        var dropped = state.Random.NextInclusive(0, 10_000) / 10_000.0 < lossChance;

        var report = new ContactReport
        {
            Id = $"contact-report-{_nextReportId++:D5}",
            ObserverUnitId = observer.Id,
            TargetUnitId = target.Id,
            Faction = observer.Faction,
            LastKnownPosition = lastKnownPosition,
            Classification = classification,
            DetectionConfidence = detectionConfidence,
            IdentificationConfidence = identificationConfidence,
            ObservedTick = state.Tick,
            CreatedTick = state.Tick,
            DeliverAtTick = state.Tick + delay,
            Channel = channel,
            Status = dropped ? ContactReportStatus.Dropped : ContactReportStatus.Queued,
            StatusReason = dropped
                ? "Report was lost in the abstract command network"
                : $"In transit via {channel}; estimated delivery T+{state.Tick + delay}"
        };
        ContactReports.Add(report);
        if (ContactReports.Count > 1000) ContactReports.RemoveRange(0, ContactReports.Count - 1000);

        if (dropped)
        {
            state.Journal.Append(state.Tick, "c2.contact-report-dropped",
                $"{observer.EntityKey} contact report for {target.EntityKey} was dropped.", observer.EntityKey, observer.Faction);
        }
        else
        {
            state.Journal.Append(state.Tick, "c2.contact-report-queued",
                $"{observer.EntityKey} queued a contact report for {target.EntityKey}; delivery T+{report.DeliverAtTick}.",
                observer.EntityKey, observer.Faction);
        }
    }

    public void ProcessReports(TacticalState state)
    {
        foreach (var report in ContactReports
                     .Where(item => item.Status == ContactReportStatus.Queued && item.DeliverAtTick <= state.Tick)
                     .OrderBy(item => item.DeliverAtTick)
                     .ThenBy(item => item.Id, StringComparer.Ordinal)
                     .ToArray())
        {
            var shared = state.Knowledge[report.Faction];
            var existing = shared.GetContact(report.TargetUnitId);
            var wasUnknown = existing is null || state.Tick - existing.LastDetectedTick > 40;
            if (existing is null)
            {
                shared.UpdateContact(new DetectionContact
                {
                    ObserverUnitId = report.ObserverUnitId,
                    TargetUnitId = report.TargetUnitId,
                    ObserverFaction = report.Faction,
                    LastKnownPosition = report.LastKnownPosition,
                    LastDetectedTick = report.ObservedTick,
                    Classification = report.Classification,
                    DetectionConfidence = report.DetectionConfidence,
                    IdentificationConfidence = report.IdentificationConfidence
                });
            }
            else if (report.ObservedTick >= existing.LastDetectedTick)
            {
                existing.ObserverUnitId = report.ObserverUnitId;
                existing.LastKnownPosition = report.LastKnownPosition;
                existing.LastDetectedTick = report.ObservedTick;
                existing.Classification = (ContactClassification)Math.Max((int)existing.Classification, (int)report.Classification);
                existing.DetectionConfidence = Math.Max(existing.DetectionConfidence * 0.75, report.DetectionConfidence);
                existing.IdentificationConfidence = Math.Max(existing.IdentificationConfidence * 0.75, report.IdentificationConfidence);
            }

            report.Status = ContactReportStatus.Delivered;
            report.DeliveredTick = state.Tick;
            report.StatusReason = "Delivered to faction-level shared picture";
            state.Journal.Append(state.Tick, "c2.contact-report-delivered",
                $"Contact report {report.Id} reached the shared {report.Faction} picture.", null, report.Faction);

            if (wasUnknown)
            {
                var targetName = state.UnitById(report.TargetUnitId)?.DisplayName ?? $"unit {report.TargetUnitId}";
                state.AddActivity(TacticalEventType.Contact,
                    $"REPORT RECEIVED: {report.Faction} command picture now carries {targetName} as {report.Classification}.",
                    report.Faction, report.ObserverUnitId);
            }
        }

        if (ContactReports.Count > 800)
        {
            ContactReports.RemoveAll(item => item.Status != ContactReportStatus.Queued &&
                state.Tick - (item.DeliveredTick ?? item.CreatedTick) > 240);
        }
    }

    public void Clear()
    {
        _localKnowledge.Clear();
        _lastReportTick.Clear();
        ContactReports.Clear();
        _nextReportId = 1;
    }
}
