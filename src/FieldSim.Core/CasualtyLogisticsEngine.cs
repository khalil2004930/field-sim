namespace FieldSim.Core;

/// <summary>
/// First-pass casualty and logistics layer. It turns wounds into treatment/evacuation states and
/// makes ammunition, water, fatigue and medical capability visible as operational readiness.
/// All timing is normalized for simulation pacing rather than real-world procedure replication.
/// </summary>
public static class CasualtyLogisticsEngine
{
    public static void Update(TacticalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var dt = Math.Max(0.05, state.SecondsPerTick);

        foreach (var unit in state.Units.Where(item => item.Soldier is not null))
        {
            UpdateSupply(unit, dt);
            UpdateCasualtyState(state, unit);
        }
    }

    private static void UpdateSupply(TacticalUnit unit, double dt)
    {
        var soldier = unit.Soldier!;
        var moving = unit.Path.Count > 0 || unit.ContinuousWaypoints.Count > 0 || unit.MovementDestinationMeters is not null;

        // Slow normalized consumption keeps long-running scenarios from treating carried water as
        // a decorative field. This is not a hydration doctrine or a real planning rate.
        var waterUse = moving ? 0.00022 : 0.00008;
        waterUse *= 1.0 + soldier.Vitals.Fatigue01 * 0.25;
        soldier.Equipment.WaterLiters = Math.Max(0, soldier.Equipment.WaterLiters - waterUse * dt);
        soldier.Vitals.Hydration01 = Math.Clamp(soldier.Equipment.WaterLiters / 2.0, 0.20, 1.0);

        var weapon = soldier.PrimaryWeapon;
        var ammo01 = weapon.InitialTotalRounds <= 0 ? 1.0 :
            Math.Clamp(weapon.TotalRounds / (double)weapon.InitialTotalRounds, 0, 1);
        var water01 = Math.Clamp(soldier.Equipment.WaterLiters / 2.0, 0, 1);
        var medical01 = soldier.Equipment.Medkit ? 1.0 : 0.55;
        soldier.SupplyReadiness01 = Math.Clamp(ammo01 * 0.62 + water01 * 0.28 + medical01 * 0.10, 0, 1);
        soldier.NeedsResupply = soldier.SupplyReadiness01 < 0.35;
    }

    private static void UpdateCasualtyState(TacticalState state, TacticalUnit unit)
    {
        var soldier = unit.Soldier!;
        var previous = soldier.CasualtyDisposition;
        var condition = soldier.Vitals.Condition;
        CasualtyDisposition next;

        if (!unit.Alive || condition.HasFlag(SoldierCondition.Dead))
        {
            next = CasualtyDisposition.Killed;
        }
        else if (soldier.IsEvacuated)
        {
            next = CasualtyDisposition.Evacuated;
        }
        else if (condition.HasFlag(SoldierCondition.Bleeding) &&
                 soldier.Vitals.Wounds.Any(wound => !wound.Treated && wound.BleedingPerMinute01 > 0.008))
        {
            next = CasualtyDisposition.NeedsTreatment;
        }
        else if (condition.HasFlag(SoldierCondition.Incapacitated) || condition.HasFlag(SoldierCondition.Unconscious))
        {
            next = soldier.EvacuationRequestId is null
                ? CasualtyDisposition.AwaitingEvacuation
                : CasualtyDisposition.EvacuationAssigned;
        }
        else if (condition.HasFlag(SoldierCondition.Wounded))
        {
            next = soldier.Vitals.Wounds.Any(wound => wound.Treated)
                ? CasualtyDisposition.Stabilized
                : CasualtyDisposition.WoundedMobile;
        }
        else
        {
            next = CasualtyDisposition.None;
        }

        if (soldier.EvacuationRequestId is { } requestId)
        {
            var request = state.OperationalSupport.Requests.FirstOrDefault(item =>
                string.Equals(item.Id, requestId, StringComparison.Ordinal));
            if (request?.Status == SupportRequestStatus.Completed)
            {
                soldier.IsEvacuated = true;
                next = CasualtyDisposition.Evacuated;
                unit.Path.Clear();
                unit.ContinuousWaypoints.Clear();
                unit.MovementDestinationMeters = null;
                unit.CurrentSpeedMetersPerSecond = 0;
                unit.CurrentAccelerationMetersPerSecondSquared = 0;
            }
            else if (request?.Status is SupportRequestStatus.Assigned or SupportRequestStatus.Executing)
            {
                next = CasualtyDisposition.EvacuationAssigned;
            }
        }

        if (next == CasualtyDisposition.AwaitingEvacuation && soldier.EvacuationRequestId is null)
            QueueEvacuation(state, unit);

        if (previous == next) return;
        soldier.CasualtyDisposition = next;
        if (next != CasualtyDisposition.None && soldier.CasualtySinceTick is null)
            soldier.CasualtySinceTick = state.Tick;
        state.AddActivity(TacticalEventType.Medical,
            $"CASUALTY STATE: {unit.DisplayName} -> {next}.", unit.Faction, unit.Id);
        state.Journal.Append(state.Tick, "medical.casualty-state",
            $"{unit.EntityKey} changed casualty state from {previous} to {next}.", unit.EntityKey, unit.Faction);
    }

    private static void QueueEvacuation(TacticalState state, TacticalUnit casualty)
    {
        var soldier = casualty.Soldier!;
        var requestId = $"casevac-{casualty.EntityKey}-{state.Tick:D6}";
        var position = state.GroundPositionOf(casualty);
        var queued = state.OperationalSupport.Queue(new SupportRequest
        {
            Id = requestId,
            RequesterEntityKey = casualty.EntityKey,
            Faction = casualty.Faction,
            Kind = SupportRequestKind.CasualtyEvacuation,
            ObjectiveCell = casualty.Position,
            ObjectivePositionMeters = position,
            Priority = 80,
            CreatedTick = state.Tick,
            StatusReason = "Casualty evacuation request created"
        }, transmissionDelayTicks: 4);
        if (!queued) return;
        soldier.EvacuationRequestId = requestId;
        state.AddActivity(TacticalEventType.Medical,
            $"EVAC REQUEST: {casualty.DisplayName} entered the support-request network.", casualty.Faction, casualty.Id);
        state.Journal.Append(state.Tick, "medical.evacuation-request",
            $"{casualty.EntityKey} queued evacuation request {requestId}.", casualty.EntityKey, casualty.Faction);
    }
}
