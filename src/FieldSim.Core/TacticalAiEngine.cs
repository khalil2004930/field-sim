namespace FieldSim.Core;

public static class TacticalAiEngine
{
    public static void AssignOrder(TacticalState state, TacticalUnit unit,
        TacticalOrderType order, GridPoint objective) =>
        AssignOrder(state, unit, order, objective, state.World.CellCenter(objective, 0));

    public static void AssignOrder(TacticalState state, TacticalUnit unit,
        TacticalOrderType order, GridPoint objective, Position3D preciseObjectiveMeters)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(unit);
        state.UnitCommands[unit.Id] = new UnitCommandState
        {
            UnitId = unit.Id,
            Order = order,
            Objective = objective,
            PreciseObjectiveMeters = preciseObjectiveMeters with
            {
                Z = state.World.GroundAltitudeAt(preciseObjectiveMeters.X, preciseObjectiveMeters.Y)
            },
            StatusText = $"{order} {WorldPointName(preciseObjectiveMeters)}"
        };
    }

    public static void Initialize(TacticalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (var unit in state.Units)
        {
            if (!state.UnitCommands.ContainsKey(unit.Id))
                AssignOrder(state, unit, TacticalOrderType.Hold, unit.Position);
        }

        state.AddActivity(TacticalEventType.Scenario,
            $"MISSION: {state.ScenarioName}. {state.MissionBriefing}");
        foreach (var unit in state.Units.Where(unit => unit.Alive))
        {
            var command = state.UnitCommands[unit.Id];
            PlanMovement(state, unit, command, force: true);
        }
    }

    public static void Plan(TacticalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (!state.AiEnabled || state.Result != BattleResult.Ongoing) return;

        if (state.Tick == 1 && state.Phase == BattlePhase.Deployment)
            SetPhase(state, BattlePhase.Advance, "Both elements began executing their pre-battle orders.");

        foreach (var unit in state.Units)
        {
            if (!state.UnitCommands.TryGetValue(unit.Id, out var command)) continue;
            if (!unit.Alive)
            {
                SetAction(state, unit, command, TacticalActionState.Disabled, "disabled");
                unit.Path.Clear();
                unit.ContinuousWaypoints.Clear();
                unit.MovementDestinationMeters = null;
                continue;
            }
            if (unit.Soldier is { IsCombatEffective: false })
            {
                SetAction(state, unit, command, TacticalActionState.Incapacitated, "incapacitated");
                unit.Path.Clear();
                unit.ContinuousWaypoints.Clear();
                unit.MovementDestinationMeters = null;
                continue;
            }
            if (state.Tick - command.LastDecisionTick < 3 &&
                (unit.Path.Count > 0 || unit.ContinuousWaypoints.Count > 0 || unit.MovementDestinationMeters is not null)) continue;
            command.LastDecisionTick = state.Tick;

            if (TryMoveMedicToCasualty(state, unit, command)) continue;
            if (unit.Soldier is { } soldier &&
                (unit.LocalCohesion01 < 0.28 || soldier.Vitals.Morale01 < 0.24) &&
                TryRegroup(state, unit, command))
                continue;
            var contact = SelectKnownContact(state, unit);
            var target = contact is null ? null : state.UnitById(contact.TargetUnitId);
            command.ContactUnitId = target?.Id;
            var localContact = target is null ? null : state.CommandAndControl.LocalFor(unit).GetContact(target.Id);
            var hasFreshLocalContact = localContact is not null && state.Tick - localContact.LastDetectedTick <= 1;

            if (unit.Soldier is not null && unit.Soldier.Vitals.Suppression01 >= 0.62 &&
                TrySeekCover(state, unit, command, contact?.LastKnownPosition))
                continue;

            if (target is not null && contact is not null && unit.Soldier is { IsCombatEffective: true })
            {
                // Shared/stale knowledge can guide a search, but it must not leak the target's
                // current authoritative position into the shooter's decision. Actual engagement
                // requires a fresh local detection from this entity on this/previous tick.
                if (hasFreshLocalContact)
                {
                    var distance = state.PositionOf(unit).HorizontalDistanceTo(state.PositionOf(target));
                    var weapon = unit.Soldier.PrimaryWeapon.Definition;
                    var los = LineOfSightEngine.Evaluate(state, unit, target);
                    if (los.State != LineOfSightState.Blocked && distance <= weapon.PracticalEngagementRangeMeters)
                    {
                        unit.Path.Clear();
                        unit.ContinuousWaypoints.Clear();
                        unit.MovementDestinationMeters = null;
                        unit.MovementProgress = 0;
                        SetAction(state, unit, command, TacticalActionState.Engaging,
                            $"engaging {target.DisplayName} at {distance:F0} m");
                        continue;
                    }

                    if (TryMoveToFiringPosition(state, unit, command, target)) continue;
                }

                if (TryMoveToContactArea(state, unit, command, contact)) continue;
                PlanMovement(state, unit, command, force: false);
                SetAction(state, unit, command, TacticalActionState.Searching,
                    $"searching last reported {contact.Classification} position ({state.Tick - contact.LastDetectedTick} ticks old)");
                continue;
            }

            PlanMovement(state, unit, command, force: false);
        }
    }

    public static void UpdateBattleState(TacticalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Result != BattleResult.Ongoing) return;
        // Utility/debug worlds can contain spatial entities without infantry physiology.
        // Do not declare a battle result when there are no soldier combatants to score.
        if (!state.Units.Any(unit => unit.Soldier is not null)) return;

        var currentContacts = state.CommandAndControl.HasRecentLocalContact(state.Tick, 3) ||
            state.Knowledge.Values.SelectMany(knowledge => knowledge.Contacts)
                .Any(contact => state.Tick - contact.LastDetectedTick <= 3);
        if (currentContacts && state.FirstContactTick is null)
        {
            state.FirstContactTick = state.Tick;
            SetPhase(state, BattlePhase.Contact, "The opposing elements made sensor contact.");
        }

        if (state.CombatEvents.Any(item => item.Tick >= state.Tick - 1 && item.Type == CombatEventType.Fire) &&
            state.Phase is BattlePhase.Advance or BattlePhase.Contact)
            SetPhase(state, BattlePhase.Engagement, "Weapons fire opened the engagement.");

        foreach (var objective in state.Objectives) UpdateObjectiveControl(state, objective);

        if (state.OpenEndedScenario) return;

        var blueEffective = EffectiveCount(state, TacticalFaction.Blue);
        var redEffective = EffectiveCount(state, TacticalFaction.Red);
        if ((blueEffective <= 1 || redEffective <= 1 || state.Tick >= 420) &&
            (int)state.Phase < (int)BattlePhase.Resolution)
            SetPhase(state, BattlePhase.Resolution, "The engagement entered its decision phase.");

        if (blueEffective == 0 && redEffective == 0)
            Finish(state, BattleResult.Draw, "Neither element remains combat effective.");
        else if (redEffective == 0 || (redEffective <= 1 && blueEffective >= 3 && state.Tick >= 90))
            Finish(state, BattleResult.BlueVictory, "Blue rendered the opposing element combat ineffective.");
        else if (blueEffective == 0 || (blueEffective <= 1 && redEffective >= 3 && state.Tick >= 90))
            Finish(state, BattleResult.RedVictory, "Red rendered the opposing element combat ineffective.");
        else if (state.Tick >= 600)
        {
            var blueScore = blueEffective * 20 + state.Objectives.Sum(objective => objective.BlueControlSeconds);
            var redScore = redEffective * 20 + state.Objectives.Sum(objective => objective.RedControlSeconds);
            Finish(state, blueScore == redScore ? BattleResult.Draw :
                blueScore > redScore ? BattleResult.BlueVictory : BattleResult.RedVictory,
                "The 10-minute scenario limit was reached; remaining combat power and objective control decided the result.");
        }
    }

    private static void UpdateObjectiveControl(TacticalState state, BattleObjective objective)
    {
        var previousBlue = objective.BlueProgress;
        var previousRed = objective.RedProgress;
        var bluePresent = HasEffectiveUnitNear(state, TacticalFaction.Blue, objective);
        var redPresent = HasEffectiveUnitNear(state, TacticalFaction.Red, objective);
        if (bluePresent && !redPresent)
        {
            objective.BlueControlSeconds++;
            objective.RedControlSeconds = Math.Max(0, objective.RedControlSeconds - 2);
        }
        else if (redPresent && !bluePresent)
        {
            objective.RedControlSeconds++;
            objective.BlueControlSeconds = Math.Max(0, objective.BlueControlSeconds - 2);
        }
        else if (bluePresent && redPresent)
        {
            objective.BlueControlSeconds = Math.Max(0, objective.BlueControlSeconds - 1);
            objective.RedControlSeconds = Math.Max(0, objective.RedControlSeconds - 1);
        }
        else
        {
            // Empty ground does not stay permanently "secured". Control memory decays slowly so
            // the AAR can distinguish a force that entered, held, then abandoned an objective.
            objective.BlueControlSeconds = Math.Max(0, objective.BlueControlSeconds - 1);
            objective.RedControlSeconds = Math.Max(0, objective.RedControlSeconds - 1);
        }

        objective.BlueProgress = ObjectiveProgress(state, objective, TacticalFaction.Blue, bluePresent, redPresent,
            objective.BlueControlSeconds, objective.BlueProgress);
        objective.RedProgress = ObjectiveProgress(state, objective, TacticalFaction.Red, redPresent, bluePresent,
            objective.RedControlSeconds, objective.RedProgress);

        LogObjectiveTransition(state, objective, TacticalFaction.Blue, previousBlue, objective.BlueProgress);
        LogObjectiveTransition(state, objective, TacticalFaction.Red, previousRed, objective.RedProgress);

        if (!state.OpenEndedScenario && objective.BlueControlSeconds >= objective.RequiredControlSeconds)
            Finish(state, BattleResult.BlueVictory, $"Blue secured {objective.DisplayName}.");
        else if (!state.OpenEndedScenario && objective.RedControlSeconds >= objective.RequiredControlSeconds)
            Finish(state, BattleResult.RedVictory, $"Red secured {objective.DisplayName}.");
    }

    private static void LogObjectiveTransition(
        TacticalState state, BattleObjective objective, TacticalFaction faction,
        ObjectiveProgressState previous, ObjectiveProgressState current)
    {
        if (previous == current) return;
        var message = $"OBJECTIVE: {faction} {objective.DisplayName} changed {previous} -> {current}.";
        state.AddActivity(TacticalEventType.Outcome, message, faction);
        state.Journal.Append(state.Tick, "objective.progress", message, objective.Id, faction);
    }

    private static ObjectiveProgressState ObjectiveProgress(
        TacticalState state, BattleObjective objective, TacticalFaction faction, bool present, bool enemyPresent,
        int controlSeconds, ObjectiveProgressState previous)
    {
        if (present && enemyPresent) return ObjectiveProgressState.Contested;
        if (present && controlSeconds >= objective.RequiredControlSeconds) return ObjectiveProgressState.Secured;
        if (present && controlSeconds >= Math.Max(1, objective.RequiredControlSeconds / 2)) return ObjectiveProgressState.Held;
        if (present && controlSeconds >= Math.Max(1, objective.RequiredControlSeconds / 4)) return ObjectiveProgressState.Clearing;
        if (present && controlSeconds > 1) return ObjectiveProgressState.EstablishingPresence;
        if (present) return ObjectiveProgressState.Entered;

        var precise = objective.PrecisePositionMeters ?? state.World.CellCenter(objective.Position, 0);
        var radius = objective.CaptureRadiusMeters ?? Math.Max(1, objective.CaptureRadiusCells) * state.World.CellSizeMeters;
        var approaching = state.Units.Any(unit => unit.Alive && unit.Faction == faction &&
            unit.Soldier is { IsCombatEffective: true } &&
            state.GroundPositionOf(unit).HorizontalDistanceTo(precise) <= radius * 2.5);
        if (approaching) return ObjectiveProgressState.Approaching;
        if (previous is ObjectiveProgressState.Entered or ObjectiveProgressState.EstablishingPresence or
            ObjectiveProgressState.Clearing or ObjectiveProgressState.Held or ObjectiveProgressState.Secured or
            ObjectiveProgressState.Contested)
            return ObjectiveProgressState.Lost;
        return ObjectiveProgressState.Unreached;
    }

    private static bool TryMoveMedicToCasualty(TacticalState state, TacticalUnit unit, UnitCommandState command)
    {
        if (unit.Soldier?.Role != SoldierRole.Medic || !unit.Soldier.Equipment.Medkit) return false;
        var unitPosition = state.GroundPositionOf(unit);
        var casualty = state.Units
            .Where(candidate => candidate.Alive && candidate.Id != unit.Id && candidate.Faction == unit.Faction &&
                                candidate.Soldier is { IsEvacuated: false } &&
                                candidate.Soldier.Vitals.Condition.HasFlag(SoldierCondition.Bleeding))
            .OrderBy(candidate => unitPosition.HorizontalDistanceTo(state.GroundPositionOf(candidate)))
            .FirstOrDefault();
        if (casualty is null) return false;

        var casualtyPosition = state.GroundPositionOf(casualty);
        var distanceMeters = unitPosition.HorizontalDistanceTo(casualtyPosition);
        if (distanceMeters <= 2.5)
        {
            unit.Path.Clear();
            unit.ContinuousWaypoints.Clear();
            unit.MovementDestinationMeters = null;
            SetAction(state, unit, command, TacticalActionState.Treating,
                $"treating {casualty.DisplayName}");
            return true;
        }

        if (!TacticalEngine.IssueMoveMeters(state, state.Units.IndexOf(unit), casualtyPosition))
            return false;
        SetAction(state, unit, command, TacticalActionState.Treating,
            $"moving to casualty {casualty.DisplayName} ({distanceMeters:F0} m)");
        return true;
    }

    private static bool TryRegroup(TacticalState state, TacticalUnit unit, UnitCommandState command)
    {
        var current = state.GroundPositionOf(unit);
        var nearby = state.Units
            .Where(candidate => candidate.Id != unit.Id && candidate.Alive && candidate.Faction == unit.Faction &&
                                candidate.Soldier is { IsCombatEffective: true } &&
                                current.HorizontalDistanceTo(state.GroundPositionOf(candidate)) <= 120)
            .ToArray();
        if (nearby.Length == 0) return false;

        var leader = nearby.FirstOrDefault(candidate =>
            candidate.Soldier?.Role is SoldierRole.Leader or SoldierRole.TeamLeader or SoldierRole.RadioOperator);
        var regroup = leader is not null
            ? state.GroundPositionOf(leader)
            : new Position3D(
                nearby.Average(candidate => state.GroundPositionOf(candidate).X),
                nearby.Average(candidate => state.GroundPositionOf(candidate).Y), 0);
        regroup = regroup with { Z = state.World.GroundAltitudeAt(regroup.X, regroup.Y) };

        if (current.HorizontalDistanceTo(regroup) <= 8)
        {
            unit.Path.Clear();
            unit.ContinuousWaypoints.Clear();
            unit.MovementDestinationMeters = null;
            SetAction(state, unit, command, TacticalActionState.Regrouping, "regrouping with nearby element");
            return true;
        }

        if (!UrbanSpatialQueries.IsWalkablePoint(state, unit, regroup) ||
            !TacticalEngine.IssueMoveMeters(state, state.Units.IndexOf(unit), regroup))
            return false;
        SetAction(state, unit, command, TacticalActionState.Regrouping, "moving to regroup with nearby element");
        return true;
    }

    private static bool TrySeekCover(TacticalState state, TacticalUnit unit,
        UnitCommandState command, Position3D? threatPosition)
    {
        var current = state.GroundPositionOf(unit);
        var candidate = MeterCandidates(state, unit, 30, 6)
            .Where(point => UrbanSpatialQueries.IsWalkablePoint(state, unit, point))
            .Select(point => new
            {
                Point = point,
                Score = UrbanSpatialQueries.CoverScoreAt(state, point) * 2.0 +
                    (threatPosition is null ? 0 : Math.Min(0.45, point.HorizontalDistanceTo(threatPosition.Value) / 250.0))
            })
            .OrderByDescending(item => item.Score)
            .ThenBy(item => current.HorizontalDistanceTo(item.Point))
            .Select(item => (Position3D?)item.Point)
            .FirstOrDefault();
        if (candidate is null || current.HorizontalDistanceTo(candidate.Value) < 1.0) return false;
        if (!TacticalEngine.IssueMoveMeters(state, state.Units.IndexOf(unit), candidate.Value)) return false;
        SetAction(state, unit, command, TacticalActionState.SeekingCover,
            "suppressed; moving to nearby synthetic cover");
        return true;
    }

    private static bool TryMoveToFiringPosition(TacticalState state, TacticalUnit unit,
        UnitCommandState command, TacticalUnit target)
    {
        var weaponRange = unit.Soldier!.PrimaryWeapon.Definition.PracticalEngagementRangeMeters;
        var targetPosition = state.PositionOf(target);
        var current = state.GroundPositionOf(unit);
        var destination = MeterCandidates(state, unit, 45, 7.5)
            .Where(point => UrbanSpatialQueries.IsWalkablePoint(state, unit, point))
            .Select(point =>
            {
                var eye = point with { Z = point.Z + unit.EyeHeightMeters };
                return new
                {
                    Point = point,
                    Los = LineOfSightEngine.Evaluate(state, eye, targetPosition).State,
                    Distance = eye.HorizontalDistanceTo(targetPosition),
                    Cover = UrbanSpatialQueries.CoverScoreAt(state, point)
                };
            })
            .Where(item => item.Los != LineOfSightState.Blocked && item.Distance <= weaponRange)
            .OrderByDescending(item => item.Cover)
            .ThenBy(item => current.HorizontalDistanceTo(item.Point))
            .Select(item => (Position3D?)item.Point)
            .FirstOrDefault();
        if (destination is null || !TacticalEngine.IssueMoveMeters(state, state.Units.IndexOf(unit), destination.Value))
            return false;
        SetAction(state, unit, command, TacticalActionState.Searching,
            $"moving through meter-native terrain for line of sight on {target.DisplayName}");
        return true;
    }

    private static IEnumerable<Position3D> MeterCandidates(
        TacticalState state, TacticalUnit unit, double radiusMeters, double spacingMeters)
    {
        var center = state.GroundPositionOf(unit);
        for (var radius = spacingMeters; radius <= radiusMeters + 0.001; radius += spacingMeters)
        {
            const int samples = 12;
            for (var i = 0; i < samples; i++)
            {
                var angle = i / (double)samples * Math.PI * 2.0;
                var x = center.X + Math.Cos(angle) * radius;
                var y = center.Y + Math.Sin(angle) * radius;
                var maxX = state.Width * state.World.CellSizeMeters;
                var maxY = state.Height * state.World.CellSizeMeters;
                if (x < 0 || y < 0 || x >= maxX || y >= maxY) continue;
                yield return new Position3D(x, y, state.World.GroundAltitudeAt(x, y));
            }
        }
    }

    private static void PlanMovement(TacticalState state, TacticalUnit unit,
        UnitCommandState command, bool force)
    {
        if (command.Order == TacticalOrderType.Hold)
        {
            unit.Path.Clear();
            unit.ContinuousWaypoints.Clear();
            unit.MovementDestinationMeters = null;
            SetAction(state, unit, command, TacticalActionState.Holding,
                $"holding {WorldPointName(state.GroundPositionOf(unit))}");
            return;
        }
        var preciseObjective = command.PreciseObjectiveMeters ?? state.World.CellCenter(command.Objective, 0);
        var metersToObjective = state.GroundPositionOf(unit).HorizontalDistanceTo(preciseObjective);
        if (metersToObjective <= 0.50)
        {
            command.ObjectiveReached = true;
            unit.Path.Clear();
            unit.ContinuousWaypoints.Clear();
            unit.MovementDestinationMeters = null;
            SetAction(state, unit, command, TacticalActionState.Holding,
                $"holding objective area {WorldPointName(preciseObjective)}");
            return;
        }
        if (!force && (unit.Path.Count > 0 || unit.ContinuousWaypoints.Count > 0 || unit.MovementDestinationMeters is not null))
        {
            SetAction(state, unit, command, TacticalActionState.Advancing,
                $"advancing to {WorldPointName(preciseObjective)}");
            return;
        }
        if (TacticalEngine.IssueMoveMeters(state, state.Units.IndexOf(unit), preciseObjective))
        {
            SetAction(state, unit, command, TacticalActionState.Advancing,
                $"advancing to {WorldPointName(preciseObjective)}");
            return;
        }
        var fallback = BestNearbyGoalMeters(state, unit, preciseObjective, 60, 10);
        if (fallback is not null && TacticalEngine.IssueMoveMeters(state, state.Units.IndexOf(unit), fallback.Value))
        {
            SetAction(state, unit, command, TacticalActionState.Advancing,
                $"advancing to nearby meter-native position at {WorldPointName(fallback.Value)}");
            return;
        }
        SetAction(state, unit, command, TacticalActionState.Holding,
            $"route blocked near {WorldPointName(state.GroundPositionOf(unit))}");
    }

    private static DetectionContact? SelectKnownContact(TacticalState state, TacticalUnit unit)
    {
        var unitPosition = state.PositionOf(unit);
        return state.CommandAndControl.ContactsKnownBy(state, unit, 25)
            .Where(contact =>
            {
                var candidate = state.UnitById(contact.TargetUnitId);
                return candidate is not null && candidate.Alive && candidate.Faction != unit.Faction;
            })
            .OrderByDescending(contact => contact.LastDetectedTick)
            .ThenByDescending(contact => contact.IdentificationConfidence)
            .ThenBy(contact => unitPosition.HorizontalDistanceTo(contact.LastKnownPosition))
            .FirstOrDefault();
    }

    private static bool TryMoveToContactArea(
        TacticalState state, TacticalUnit unit, UnitCommandState command, DetectionContact contact)
    {
        var current = state.GroundPositionOf(unit);
        var reported = contact.LastKnownPosition with
        {
            Z = state.World.GroundAltitudeAt(contact.LastKnownPosition.X, contact.LastKnownPosition.Y)
        };
        if (current.HorizontalDistanceTo(reported) <= 12)
        {
            unit.Path.Clear();
            unit.ContinuousWaypoints.Clear();
            unit.MovementDestinationMeters = null;
            SetAction(state, unit, command, TacticalActionState.Searching,
                $"searching last reported {contact.Classification} area");
            return true;
        }

        Position3D? destination = UrbanSpatialQueries.IsWalkablePoint(state, unit, reported)
            ? reported
            : BestNearbyGoalMeters(state, unit, reported, 35, 7);
        if (destination is null || !TacticalEngine.IssueMoveMeters(state, state.Units.IndexOf(unit), destination.Value))
            return false;

        SetAction(state, unit, command, TacticalActionState.Searching,
            $"moving toward last reported {contact.Classification} position ({state.Tick - contact.LastDetectedTick} ticks old)");
        return true;
    }

    private static Position3D? BestNearbyGoalMeters(
        TacticalState state, TacticalUnit unit, Position3D objective, double radiusMeters, double spacingMeters)
    {
        var candidates = new List<Position3D>();
        for (var radius = spacingMeters; radius <= radiusMeters + 0.001; radius += spacingMeters)
        {
            const int samples = 16;
            for (var i = 0; i < samples; i++)
            {
                var angle = i / (double)samples * Math.PI * 2.0;
                var x = objective.X + Math.Cos(angle) * radius;
                var y = objective.Y + Math.Sin(angle) * radius;
                var point = new Position3D(x, y, state.World.GroundAltitudeAt(x, y));
                if (!UrbanSpatialQueries.IsWalkablePoint(state, unit, point)) continue;
                candidates.Add(point);
            }
        }

        return candidates
            .OrderBy(point => point.HorizontalDistanceTo(objective))
            .ThenByDescending(point => UrbanSpatialQueries.CoverScoreAt(state, point))
            .Select(point => (Position3D?)point)
            .FirstOrDefault();
    }

    private static bool HasEffectiveUnitNear(TacticalState state, TacticalFaction faction, BattleObjective objective)
    {
        if (objective.PrecisePositionMeters is { } precise && objective.CaptureRadiusMeters is { } radiusMeters)
        {
            return state.Units.Any(unit => unit.Alive && unit.Faction == faction &&
                unit.Soldier is { IsCombatEffective: true } &&
                state.GroundPositionOf(unit).HorizontalDistanceTo(precise) <= radiusMeters);
        }

        return state.Units.Any(unit => unit.Alive && unit.Faction == faction &&
            unit.Soldier is { IsCombatEffective: true } &&
            Manhattan(unit.Position, objective.Position) <= objective.CaptureRadiusCells);
    }

    private static int EffectiveCount(TacticalState state, TacticalFaction faction) =>
        state.Units.Count(unit => unit.Alive && unit.Faction == faction &&
                                  unit.Soldier is { IsCombatEffective: true });

    private static void SetAction(TacticalState state, TacticalUnit unit, UnitCommandState command,
        TacticalActionState action, string status)
    {
        var changed = command.Action != action || !string.Equals(command.StatusText, status, StringComparison.Ordinal);
        command.Action = action;
        command.StatusText = status;
        if (!changed || state.Tick - command.LastActionChangeTick < 3) return;
        command.LastActionChangeTick = state.Tick;
        state.AddActivity(action == TacticalActionState.Treating ? TacticalEventType.Medical : TacticalEventType.Movement,
            $"{unit.DisplayName}: {status}.", unit.Faction, unit.Id);
    }

    private static void SetPhase(TacticalState state, BattlePhase phase, string message)
    {
        if (state.Phase == phase || state.Phase == BattlePhase.Complete) return;
        state.Phase = phase;
        state.AddActivity(TacticalEventType.Phase, $"PHASE {phase.ToString().ToUpperInvariant()}: {message}");
    }

    private static void Finish(TacticalState state, BattleResult result, string reason)
    {
        if (state.Result != BattleResult.Ongoing) return;
        state.Result = result;
        state.Phase = BattlePhase.Complete;
        foreach (var unit in state.Units)
        {
            unit.Path.Clear();
            unit.ContinuousWaypoints.Clear();
            unit.MovementDestinationMeters = null;
        }
        state.AddActivity(TacticalEventType.Outcome, $"RESULT {result}: {reason}");
    }

    private static string WorldPointName(Position3D point) =>
        $"X {point.X / 1000.0:F2} km / Y {point.Y / 1000.0:F2} km";

    // Legacy-only planning-cell name retained for old debug/test helpers.
    private static string CellName(GridPoint point) =>
        $"{(char)('A' + Math.Clamp(point.Y, 0, 25))}{point.X + 1:00}";

    private static int Manhattan(GridPoint first, GridPoint second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);
}
