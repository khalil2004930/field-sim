namespace FieldSim.Core;

public static class EngagementEngine
{
    public static void Step(TacticalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var dt = Math.Max(0.05, state.SecondsPerTick);

        foreach (var unit in state.Units.Where(unit => unit.Alive && unit.Soldier is { IsEvacuated: false }))
        {
            UpdatePhysiology(state, unit, dt);
            UpdateWeapon(unit.Soldier!.PrimaryWeapon, dt);
        }

        if (state.AiEnabled)
        {
            foreach (var shooter in state.Units.Where(unit =>
                         unit.Alive && unit.Soldier is { IsCombatEffective: true }))
            {
                TryEngage(state, shooter);
            }
        }

        ResolveMedicalSupport(state, dt);
        if (state.CombatEvents.Count > 2000)
            state.CombatEvents.RemoveRange(0, state.CombatEvents.Count - 2000);
    }

    private static void TryEngage(TacticalState state, TacticalUnit shooter)
    {
        var soldier = shooter.Soldier!;
        var weapon = soldier.PrimaryWeapon;
        if (weapon.Malfunctioned || weapon.ReloadSecondsRemaining > 0 || weapon.CooldownSeconds > 0)
            return;

        var engagementTarget = SelectKnownTarget(state, shooter, weapon.Definition.PracticalEngagementRangeMeters);
        if (engagementTarget is null) return;
        var target = engagementTarget.Unit;

        if (weapon.RoundsLoaded <= 0)
        {
            StartReload(state, shooter, weapon);
            return;
        }

        // Normalized simulation-only reliability model. This does not reproduce a real
        // weapon's measured malfunction rate; it simply makes heat/reliability state matter.
        var malfunctionChance = Math.Clamp((1.0 - weapon.Definition.Reliability01) * 0.12 + weapon.Heat01 * 0.006, 0, 0.03);
        if (Next01(state.Random) < malfunctionChance)
        {
            weapon.Malfunctioned = true;
            weapon.MalfunctionSecondsRemaining = 2.0 + Next01(state.Random) * 4.0;
            state.CombatEvents.Add(new CombatEvent(state.Tick, shooter.Id, null,
                $"{shooter.DisplayName} is clearing a weapon malfunction.", CombatEventType.System));
            return;
        }

        var los = engagementTarget.Los;
        var distance = engagementTarget.DistanceMeters;
        if (distance > weapon.Definition.MaximumPhysicalRangeMeters) return;

        var shots = DetermineBurstSize(weapon);
        shots = Math.Min(shots, weapon.RoundsLoaded);
        if (shots <= 0) return;

        weapon.RoundsLoaded -= shots;
        var cyclicBurstSeconds = shots * 60.0 / Math.Max(1, weapon.Definition.CyclicRateRpm);
        var sustainedRecoverySeconds = shots * 60.0 / Math.Max(1, weapon.Definition.SustainedRateRpm);
        weapon.CooldownSeconds = Math.Max(0.12, Math.Max(cyclicBurstSeconds, sustainedRecoverySeconds));
        weapon.Heat01 = Math.Clamp(weapon.Heat01 + shots * 0.012, 0, 1);

        var hitCount = 0;
        for (var shot = 0; shot < shots; shot++)
        {
            if (RollShot(state, shooter, target, los, distance))
            {
                ApplyHit(state, shooter, target, distance);
                hitCount++;
                if (!target.Alive) break;
            }
            else
            {
                ApplySuppression(target, weapon.Definition.Suppression01 * 0.065);
            }
        }

        ApplySuppression(target, weapon.Definition.Suppression01 * Math.Min(0.22, shots * 0.035));
        state.CombatEvents.Add(new CombatEvent(state.Tick, shooter.Id, target.Id,
            $"{shooter.DisplayName} fired {shots} round{(shots == 1 ? "" : "s")} at {target.DisplayName}" +
            (hitCount > 0 ? $"; {hitCount} hit{(hitCount == 1 ? "" : "s")}." : "."),
            CombatEventType.Fire));

        if (weapon.RoundsLoaded == 0 && weapon.ReserveRounds > 0)
            StartReload(state, shooter, weapon);
    }

    private static EngagementTarget? SelectKnownTarget(TacticalState state, TacticalUnit shooter, double rangeMeters)
    {
        var shooterPosition = state.PositionOf(shooter);
        var candidateIds = state.SpatialIndex.QueryRadius(state.GroundPositionOf(shooter), rangeMeters);
        state.Performance.RecordSpatialQuery(candidateIds.Count);
        EngagementTarget? best = null;
        foreach (var targetId in candidateIds)
        {
            var target = state.UnitById(targetId);
            if (target is null || !target.Alive || target.Soldier is not { IsEvacuated: false } || target.Faction == shooter.Faction || target.Id == shooter.Id)
                continue;
            // Shared command-picture knowledge can guide movement/search, but firing requires
            // this shooter to have a fresh local sensor contact. This prevents stale/shared
            // knowledge from leaking the target's current authoritative position into combat.
            var localContact = state.CommandAndControl.LocalFor(shooter).GetContact(target.Id);
            if (localContact is null || state.Tick - localContact.LastDetectedTick > 1) continue;
            var distance = shooterPosition.HorizontalDistanceTo(state.PositionOf(target));
            if (distance > rangeMeters || best is not null && distance >= best.DistanceMeters) continue;
            var los = LineOfSightEngine.Evaluate(state, shooter, target);
            if (los.State == LineOfSightState.Blocked) continue;
            best = new EngagementTarget(target, los, distance);
        }
        return best;
    }

    private sealed record EngagementTarget(
        TacticalUnit Unit,
        LineOfSightResult Los,
        double DistanceMeters);

    private static int DetermineBurstSize(WeaponRuntime weapon)
    {
        return weapon.SelectedFireMode switch
        {
            FireMode.Automatic when weapon.Definition.Class is InfantryWeaponClass.LightMachineGun or InfantryWeaponClass.GeneralPurposeMachineGun => 5,
            FireMode.Automatic => 3,
            FireMode.Burst => 3,
            _ => 1
        };
    }

    private static bool RollShot(
        TacticalState state,
        TacticalUnit shooter,
        TacticalUnit target,
        LineOfSightResult los,
        double distanceMeters)
    {
        var soldier = shooter.Soldier!;
        var weapon = soldier.PrimaryWeapon;
        var definition = weapon.Definition;
        var optic = weapon.Optic;
        var targetContext = state.ContextOf(target);

        var rangeFraction = Math.Clamp(distanceMeters / Math.Max(1, definition.PracticalEngagementRangeMeters), 0, 2.0);
        var rangeFactor = Math.Clamp(1.0 - rangeFraction * 0.58, 0.06, 1.0);
        var precision = definition.Precision01 * 0.42 + definition.Handling01 * 0.12;
        var skill = soldier.Skills.Marksmanship01 * 0.26;
        var opticFactor = AcquisitionQuality(state.Environment, optic) * 0.16;
        var postureAndState = 1.0 - soldier.Vitals.Suppression01 * 0.42 - soldier.Vitals.Fatigue01 * 0.24;
        var exposure = 1.0 - targetContext.Concealment * 0.38 - targetContext.Cover * 0.30;
        var obscuration = 1.0 - los.ObscurationFactor * 0.50;
        var movementPenalty = shooter.Path.Count > 0 || shooter.MovementDestinationMeters is not null ? 0.72 : 1.0;
        var targetMovementPenalty = target.Path.Count > 0 || target.MovementDestinationMeters is not null ? 0.86 : 1.0;
        var heatPenalty = Math.Clamp(1.0 - weapon.Heat01 * 0.10, 0.85, 1.0);

        // Synthetic game relationship, not a real-world ballistic firing solution.
        var probability = (precision + skill + opticFactor) * rangeFactor * postureAndState * exposure * obscuration * movementPenalty * targetMovementPenalty * heatPenalty;
        probability = Math.Clamp(probability * 0.58, 0.015, 0.78);
        return Next01(state.Random) <= probability;
    }

    private static double AcquisitionQuality(EnvironmentState environment, OpticProfile optic)
    {
        var ambient = environment.AmbientLight01;
        var weather = Math.Clamp(environment.VisibilityMeters / 4000.0, 0.12, 1.0);
        var quality = optic.AcquisitionQuality01;

        if (optic.Thermal)
        {
            quality *= 0.78 + environment.ThermalContrast01 * 0.42;
            if (environment.Visibility is WeatherVisibility.Rain or WeatherVisibility.Fog)
                quality *= 0.78;
        }
        else if (optic.NightVision)
        {
            quality *= 0.58 + Math.Sqrt(Math.Max(ambient, 0.02)) * 0.70;
            if (environment.Visibility is WeatherVisibility.Fog or WeatherVisibility.Dust)
                quality *= 0.72;
        }
        else
        {
            quality *= 0.30 + ambient * 0.70;
        }

        return Math.Clamp(quality * weather, 0.08, 1.0);
    }

    private static void ApplyHit(TacticalState state, TacticalUnit shooter, TacticalUnit target, double distanceMeters)
    {
        if (target.Soldier is null)
        {
            target.Alive = false;
            state.CombatEvents.Add(new CombatEvent(state.Tick, shooter.Id, target.Id,
                $"{target.DisplayName} was disabled.", CombatEventType.Disabled));
            return;
        }

        var soldier = target.Soldier;
        var region = RollBodyRegion(state.Random);
        var armor = soldier.Equipment.Armor;
        var protectedRegion = region is BodyRegion.Chest or BodyRegion.Abdomen or BodyRegion.Head;
        var coverage = region == BodyRegion.Head ? armor.HeadCoverage01 : armor.TorsoCoverage01;
        var armorCaught = protectedRegion && Next01(state.Random) < coverage;

        var baseSeverity = region switch
        {
            BodyRegion.Head or BodyRegion.Neck => 0.72,
            BodyRegion.Chest => 0.58,
            BodyRegion.Abdomen or BodyRegion.Pelvis => 0.50,
            _ => 0.34
        };
        var rangeAttenuation = Math.Clamp(1.0 - distanceMeters / 6000.0, 0.55, 1.0);
        var severity = baseSeverity * rangeAttenuation * (0.78 + Next01(state.Random) * 0.38);
        var woundType = WoundType.Penetrating;

        if (armorCaught)
        {
            var residual = Math.Clamp(1.0 - armor.Protection01, 0.12, 0.72);
            severity *= residual;
            woundType = WoundType.BluntTrauma;
        }

        severity = Math.Clamp(severity, 0.08, 1.0);
        var wound = new Wound
        {
            Region = region,
            Type = woundType,
            Severity01 = severity,
            BleedingPerMinute01 = woundType == WoundType.BluntTrauma ? severity * 0.025 : severity * 0.085,
            Pain01 = Math.Clamp(severity * 0.88, 0, 1),
            MobilityPenalty01 = region is BodyRegion.LeftLeg or BodyRegion.RightLeg ? severity * 0.70 :
                region is BodyRegion.LeftArm or BodyRegion.RightArm ? severity * 0.28 : severity * 0.12
        };
        soldier.Vitals.Wounds.Add(wound);
        soldier.TimeSinceLastHitSeconds = 0;
        soldier.Vitals.HitPoints = Math.Max(0, soldier.Vitals.HitPoints - severity * 42);
        soldier.Vitals.Pain01 = Math.Clamp(soldier.Vitals.Pain01 + wound.Pain01 * 0.48, 0, 1);
        soldier.Vitals.Shock01 = Math.Clamp(soldier.Vitals.Shock01 + severity * 0.22, 0, 1);
        soldier.Vitals.Condition |= SoldierCondition.Wounded;
        if (wound.BleedingPerMinute01 > 0.008) soldier.Vitals.Condition |= SoldierCondition.Bleeding;
        ApplySuppression(target, 0.35 + severity * 0.25);

        state.CombatEvents.Add(new CombatEvent(state.Tick, shooter.Id, target.Id,
            $"{target.DisplayName} hit: {region}, {woundType}, severity {severity:P0}.",
            CombatEventType.Hit));
        var wasAlive = target.Alive;
        RecalculateCondition(target);
        if (wasAlive && !target.Alive)
            state.CombatEvents.Add(new CombatEvent(state.Tick, shooter.Id, target.Id,
                $"{target.DisplayName} is dead.", CombatEventType.Casualty));
    }

    private static void UpdatePhysiology(TacticalState state, TacticalUnit unit, double dt)
    {
        var soldier = unit.Soldier!;
        var vitals = soldier.Vitals;
        soldier.TimeSinceLastHitSeconds += dt;
        soldier.TimeSinceTreatmentSeconds += dt;

        var bleedingPerMinute = vitals.Wounds.Where(wound => !wound.Treated)
            .Sum(wound => wound.BleedingPerMinute01);
        vitals.BloodVolume01 = Math.Clamp(vitals.BloodVolume01 - bleedingPerMinute * dt / 60.0, 0, 1);
        vitals.Suppression01 = Math.Max(0, vitals.Suppression01 - dt * 0.018 * (0.55 + soldier.Skills.Discipline01));
        vitals.Pain01 = Math.Max(0, vitals.Pain01 - dt * 0.0025);

        var loadRatio = soldier.CarriedMassKg / Math.Max(45, soldier.BaseBodyMassKg);
        var moving = unit.Path.Count > 0 || unit.ContinuousWaypoints.Count > 0 || unit.MovementDestinationMeters is not null;
        var hydrationPenalty = 1.0 + (1.0 - vitals.Hydration01) * 0.45;
        if (moving)
            vitals.Fatigue01 = Math.Clamp(vitals.Fatigue01 + dt * 0.0020 * (1 + loadRatio) * hydrationPenalty, 0, 1);
        else
            vitals.Fatigue01 = Math.Max(0, vitals.Fatigue01 - dt * 0.0015 * (0.5 + soldier.Skills.Fitness01) / hydrationPenalty);

        vitals.Shock01 = Math.Clamp(
            vitals.Shock01 + Math.Max(0, 0.78 - vitals.BloodVolume01) * dt * 0.012 - dt * 0.001,
            0, 1);
        vitals.Consciousness01 = Math.Clamp(1.0 - vitals.Shock01 * 0.70 - vitals.Pain01 * 0.18 -
            Math.Max(0, 0.68 - vitals.BloodVolume01) * 1.25, 0, 1);
        vitals.HitPoints = Math.Max(0, vitals.HitPoints - Math.Max(0, 0.55 - vitals.BloodVolume01) * dt * 1.1);

        var wasAlive = unit.Alive;
        RecalculateCondition(unit);
        if (wasAlive && !unit.Alive)
            state.CombatEvents.Add(new CombatEvent(state.Tick, null, unit.Id,
                $"{unit.DisplayName} is dead.", CombatEventType.Casualty));
    }

    private static void RecalculateCondition(TacticalUnit unit)
    {
        var soldier = unit.Soldier!;
        var vitals = soldier.Vitals;
        var condition = SoldierCondition.None;

        if (vitals.Fatigue01 >= 0.65) condition |= SoldierCondition.Fatigued;
        if (vitals.Suppression01 >= 0.28) condition |= SoldierCondition.Suppressed;
        if (vitals.Wounds.Count > 0) condition |= SoldierCondition.Wounded;
        if (vitals.Wounds.Any(wound => !wound.Treated && wound.BleedingPerMinute01 > 0.008))
            condition |= SoldierCondition.Bleeding;
        if (vitals.Shock01 >= 0.48) condition |= SoldierCondition.InShock;
        if (vitals.Consciousness01 <= 0.28) condition |= SoldierCondition.Unconscious;
        if (vitals.HitPoints <= 22 || vitals.BloodVolume01 <= 0.52 ||
            vitals.Wounds.Any(wound => wound.Severity01 >= 0.88))
            condition |= SoldierCondition.Incapacitated;
        if (vitals.HitPoints <= 0 || vitals.BloodVolume01 <= 0.24)
            condition |= SoldierCondition.Dead;

        vitals.Condition = condition;
        unit.Alive = !condition.HasFlag(SoldierCondition.Dead);
    }

    private static void UpdateWeapon(WeaponRuntime weapon, double dt)
    {
        weapon.CooldownSeconds = Math.Max(0, weapon.CooldownSeconds - dt);
        weapon.Heat01 = Math.Max(0, weapon.Heat01 - dt * 0.025);
        if (weapon.Malfunctioned)
        {
            weapon.MalfunctionSecondsRemaining = Math.Max(0, weapon.MalfunctionSecondsRemaining - dt);
            if (weapon.MalfunctionSecondsRemaining <= 0) weapon.Malfunctioned = false;
        }
        if (weapon.ReloadSecondsRemaining > 0)
        {
            weapon.ReloadSecondsRemaining = Math.Max(0, weapon.ReloadSecondsRemaining - dt);
            if (weapon.ReloadSecondsRemaining <= 0 && weapon.RoundsLoaded == 0 && weapon.ReserveRounds > 0)
            {
                var loaded = Math.Min(weapon.Definition.MagazineCapacity, weapon.ReserveRounds);
                weapon.RoundsLoaded = loaded;
                weapon.ReserveRounds -= loaded;
            }
        }
    }

    private static void StartReload(TacticalState state, TacticalUnit unit, WeaponRuntime weapon)
    {
        if (weapon.ReserveRounds <= 0 || weapon.ReloadSecondsRemaining > 0) return;
        weapon.ReloadSecondsRemaining = weapon.Definition.BaseReloadSeconds *
            (1.0 + (unit.Soldier?.Vitals.Fatigue01 ?? 0) * 0.45 + (unit.Soldier?.Vitals.Suppression01 ?? 0) * 0.35);
        state.CombatEvents.Add(new CombatEvent(state.Tick, unit.Id, null,
            $"{unit.DisplayName} is reloading.", CombatEventType.Reload));
    }

    private static void ResolveMedicalSupport(TacticalState state, double dt)
    {
        foreach (var casualty in state.Units.Where(unit => unit.Alive && unit.Soldier is not null &&
                     unit.Soldier.Vitals.Condition.HasFlag(SoldierCondition.Bleeding)))
        {
            var patient = casualty.Soldier!;
            if (patient.TimeSinceTreatmentSeconds < 18 || patient.Vitals.Suppression01 > 0.55) continue;

            var medic = state.Units
                .Where(unit => unit.Alive && unit.Faction == casualty.Faction && unit.Soldier is not null && unit.Id != casualty.Id)
                .Where(unit => unit.Soldier!.IsCombatEffective && unit.Soldier.Equipment.Medkit)
                .Where(unit => state.GroundPositionOf(unit).HorizontalDistanceTo(state.GroundPositionOf(casualty)) <= 2.5)
                .OrderByDescending(unit => unit.Soldier!.Skills.Medical01)
                .FirstOrDefault();

            var selfAid = patient.Equipment.Medkit && patient.IsCombatEffective && patient.Vitals.Suppression01 < 0.32;
            if (medic is null && !selfAid) continue;

            var skill = medic?.Soldier?.Skills.Medical01 ?? Math.Max(0.28, patient.Skills.Medical01);
            var wound = patient.Vitals.Wounds
                .Where(item => !item.Treated && item.BleedingPerMinute01 > 0)
                .OrderByDescending(item => item.BleedingPerMinute01)
                .FirstOrDefault();
            if (wound is null) continue;

            var chancePerSecond = 0.025 + skill * 0.035;
            if (Next01(state.Random) > chancePerSecond * dt) continue;

            wound.Treated = true;
            wound.BleedingPerMinute01 *= 0.12;
            patient.TimeSinceTreatmentSeconds = 0;
            patient.Vitals.Shock01 = Math.Max(0, patient.Vitals.Shock01 - 0.06);
            state.CombatEvents.Add(new CombatEvent(state.Tick, medic?.Id ?? casualty.Id, casualty.Id,
                $"{casualty.DisplayName} received medical stabilization.", CombatEventType.Medical));
            RecalculateCondition(casualty);
        }
    }

    private static void ApplySuppression(TacticalUnit target, double amount)
    {
        if (target.Soldier is null) return;
        target.Soldier.Vitals.Suppression01 = Math.Clamp(target.Soldier.Vitals.Suppression01 + amount, 0, 1);
    }

    private static BodyRegion RollBodyRegion(DeterministicRng random)
    {
        var roll = random.NextInclusive(1, 100);
        return roll switch
        {
            <= 8 => BodyRegion.Head,
            <= 11 => BodyRegion.Neck,
            <= 36 => BodyRegion.Chest,
            <= 52 => BodyRegion.Abdomen,
            <= 59 => BodyRegion.Pelvis,
            <= 69 => BodyRegion.LeftArm,
            <= 79 => BodyRegion.RightArm,
            <= 89 => BodyRegion.LeftLeg,
            _ => BodyRegion.RightLeg
        };
    }

    private static double Next01(DeterministicRng random) => random.NextInclusive(0, 10_000) / 10_000.0;

    private static int Manhattan(GridPoint a, GridPoint b) => Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
}
