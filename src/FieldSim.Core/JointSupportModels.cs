namespace FieldSim.Core;

public enum AirMissionRole
{
    FrontlineIsr,
    BorderResponse,
    CombatAirPatrol,
    GroundReady,
    Turnaround,
    StaticSupport
}

public enum SupportMissionKind
{
    CounterBatteryCue,
    DroneStrike,
    FixedWingStrike,
    TubeArtillery,
    RocketFire,
    MortarFire,
    Observation
}

public enum SupportMissionState
{
    Cued,
    Evaluating,
    Assigned,
    Executing,
    Completed,
    HeldForFriendlyRisk,
    NoAssetAvailable
}

public enum SupportImpactKind
{
    Artillery,
    Rocket,
    Mortar,
    DroneStrike,
    Airstrike
}

public sealed class FriendlyTrackEstimate
{
    public required string EntityKey { get; init; }
    public required Position3D LastKnownPosition { get; init; }
    public required long LastUpdateTick { get; init; }
    public double BaseUncertaintyMeters { get; init; } = 12;
    public double UncertaintyGrowthMetersPerSecond { get; init; } = 1.5;
    public bool CommunicationsConnected { get; init; } = true;

    public double UncertaintyAt(long tick, double secondsPerTick)
    {
        var ageSeconds = Math.Max(0, tick - LastUpdateTick) * Math.Max(0.01, secondsPerTick);
        var growth = CommunicationsConnected ? 0.25 : UncertaintyGrowthMetersPerSecond;
        return BaseUncertaintyMeters + ageSeconds * growth;
    }
}

public sealed class CounterBatteryCue
{
    public required string Id { get; init; }
    public required string SensorId { get; init; }
    public required Position3D EstimatedOrigin { get; init; }
    public required double UncertaintyRadiusMeters { get; init; }
    public required long CreatedTick { get; init; }
    public string SourceDescription { get; init; } = "Indirect-fire launch signature";
    public bool Resolved { get; set; }
}

public sealed class JointSupportAssetState
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required TacticalFaction Faction { get; init; }
    public required SupportAssetKind Kind { get; init; }
    public required AirMissionRole Role { get; set; }
    public required string OperatingZoneId { get; init; }
    public string? OrbatNodeId { get; init; }
    public string ZoneLabel { get; init; } = "Synthetic support zone";
    public string PayloadClass { get; init; } = "none";
    public string MapSymbol { get; init; } = "SUP";

    public Position3D Position { get; set; }
    public Position3D HomePosition { get; init; }
    public Position3D OrbitCenter { get; init; }
    public double OrbitRadiusMeters { get; init; }
    public double OrbitPhaseRadians { get; init; }
    public Position3D? MissionTarget { get; set; }
    public double HeadingDegrees { get; set; }
    public double CurrentSpeedMetersPerSecond { get; set; }
    public double CurrentAccelerationMetersPerSecondSquared { get; set; }
    public double MaxSpeedMetersPerSecond { get; init; }
    public double AccelerationMetersPerSecondSquared { get; init; }
    public double DecelerationMetersPerSecondSquared { get; init; }

    private double _fuelPercent = 100;
    public int FuelPercent
    {
        get => (int)Math.Round(_fuelPercent);
        set => _fuelPercent = Math.Clamp(value, 0, 100);
    }
    public double FuelPercentExact
    {
        get => _fuelPercent;
        set => _fuelPercent = Math.Clamp(value, 0, 100);
    }

    public int StoresRemaining { get; set; }
    public bool Armed => StoresRemaining > 0;
    public bool Available { get; set; } = true;
    public string StatusText { get; set; } = "Ready";
    public long? BusyUntilTick { get; set; }
    public bool IsAirborne => Role is AirMissionRole.FrontlineIsr or AirMissionRole.BorderResponse or AirMissionRole.CombatAirPatrol;
    // StaticSupport means the asset is emplaced for this scenario even if the underlying
    // platform has a road-mobility capability. Its max-speed field remains useful reference
    // state, but a fire mission must never make the gun/launcher drive toward the target.
    public bool IsStatic => Role == AirMissionRole.StaticSupport || MaxSpeedMetersPerSecond <= 0.01;
}

public sealed class JointSupportMission
{
    public required string Id { get; init; }
    public required SupportMissionKind Kind { get; set; }
    public required TacticalFaction RequestingFaction { get; init; }
    public required Position3D EstimatedTarget { get; init; }
    public double TargetUncertaintyRadiusMeters { get; init; }
    public long CreatedTick { get; init; }
    public SupportMissionState State { get; set; } = SupportMissionState.Cued;
    public string? AssignedAssetId { get; set; }
    public string Reason { get; set; } = "Awaiting evaluation";
    public double FriendlyRiskScore { get; set; }
}

public sealed class SupportImpactMarker
{
    public required string Id { get; init; }
    public required SupportImpactKind Kind { get; init; }
    public required TacticalFaction Faction { get; init; }
    public required Position3D Position { get; init; }
    public required long CreatedTick { get; init; }
    public required string Label { get; init; }
    public string? SourceAssetId { get; init; }
}

public sealed class JointSupportState
{
    private long _nextCue = 1;
    private long _nextMission = 1;
    private long _nextImpact = 1;
    public List<JointSupportAssetState> Assets { get; } = [];
    public List<CounterBatteryCue> CounterBatteryCues { get; } = [];
    public List<JointSupportMission> Missions { get; } = [];
    public List<SupportImpactMarker> Impacts { get; } = [];

    public CounterBatteryCue AddCounterBatteryCue(
        TacticalState state,
        string sensorId,
        Position3D syntheticLaunchOrigin,
        string sourceDescription = "Indirect-fire launch signature")
    {
        // Intentionally imprecise game-level cue. This is not a real radar error model.
        var uncertainty = 90 + NextUnitInterval(state.Random) * 80;
        var angle = NextUnitInterval(state.Random) * Math.PI * 2;
        var radialError = NextUnitInterval(state.Random) * uncertainty * 0.7;
        var estimate = new Position3D(
            syntheticLaunchOrigin.X + Math.Cos(angle) * radialError,
            syntheticLaunchOrigin.Y + Math.Sin(angle) * radialError,
            syntheticLaunchOrigin.Z);
        var cue = new CounterBatteryCue
        {
            Id = $"cb-cue-{_nextCue++:D4}",
            SensorId = sensorId,
            EstimatedOrigin = estimate,
            UncertaintyRadiusMeters = uncertainty,
            CreatedTick = state.Tick,
            SourceDescription = sourceDescription
        };
        CounterBatteryCues.Add(cue);
        if (CounterBatteryCues.Count > 80) CounterBatteryCues.RemoveAt(0);
        return cue;
    }

    public JointSupportMission CreateCounterBatteryMission(TacticalState state, CounterBatteryCue cue)
    {
        var mission = new JointSupportMission
        {
            Id = $"support-mission-{_nextMission++:D4}",
            Kind = SupportMissionKind.CounterBatteryCue,
            RequestingFaction = TacticalFaction.Blue,
            EstimatedTarget = cue.EstimatedOrigin,
            TargetUncertaintyRadiusMeters = cue.UncertaintyRadiusMeters,
            CreatedTick = state.Tick,
            Reason = "Radar cue awaiting ISR/strike asset evaluation"
        };
        Missions.Add(mission);
        cue.Resolved = true;
        if (Missions.Count > 120) Missions.RemoveAt(0);
        return mission;
    }

    public JointSupportMission CreateTubeArtilleryMission(
        TacticalState state,
        Position3D syntheticTarget,
        IReadOnlyList<FriendlyTrackEstimate> friendlyTracks)
    {
        var mission = new JointSupportMission
        {
            Id = $"support-mission-{_nextMission++:D4}",
            Kind = SupportMissionKind.TubeArtillery,
            RequestingFaction = TacticalFaction.Blue,
            EstimatedTarget = syntheticTarget,
            TargetUncertaintyRadiusMeters = 55,
            CreatedTick = state.Tick,
            Reason = "Synthetic tube-artillery request awaiting friendly-risk screening"
        };
        Missions.Add(mission);
        if (Missions.Count > 120) Missions.RemoveAt(0);

        mission.State = SupportMissionState.Evaluating;
        mission.FriendlyRiskScore = EstimateFriendlyRisk(state, mission, friendlyTracks);
        if (mission.FriendlyRiskScore >= 0.62)
        {
            mission.State = SupportMissionState.HeldForFriendlyRisk;
            mission.Reason = "M109 mission held: friendly-position uncertainty overlaps the abstract effects envelope";
            return mission;
        }

        var selected = Assets
            .Where(asset => asset.Faction == TacticalFaction.Blue && asset.Kind == SupportAssetKind.TubeArtillery &&
                            asset.Available && asset.StoresRemaining > 0)
            .OrderBy(asset => asset.Position.HorizontalDistanceTo(syntheticTarget))
            .FirstOrDefault();
        if (selected is null)
        {
            mission.State = SupportMissionState.NoAssetAvailable;
            mission.Reason = "No Blue tube-artillery support element is available or supplied";
            return mission;
        }

        AssignMission(state, mission, selected, busyTicks: 18, fuelCostPercent: 0, storeCost: 1,
            reason: $"{selected.DisplayName} assigned after abstract friendly-risk screening");
        return mission;
    }

    public void ProcessCounterBatteryMission(
        TacticalState state,
        JointSupportMission mission,
        IReadOnlyList<FriendlyTrackEstimate> friendlyTracks)
    {
        if (mission.State is SupportMissionState.Completed or SupportMissionState.Executing or
            SupportMissionState.HeldForFriendlyRisk) return;

        mission.State = SupportMissionState.Evaluating;
        mission.FriendlyRiskScore = EstimateFriendlyRisk(state, mission, friendlyTracks);
        if (mission.FriendlyRiskScore >= 0.62)
        {
            mission.State = SupportMissionState.HeldForFriendlyRisk;
            mission.Reason = "Strike held: friendly-position uncertainty overlaps the target-risk envelope";
            return;
        }

        // Game-level allocation priority: preserve frontline ISR, use dedicated response UAS first,
        // then ground-ready UAS, then airborne fixed-wing support as fallback.
        var selected = Assets
            .Where(asset => asset.Faction == TacticalFaction.Blue && asset.Available && asset.Armed && asset.FuelPercent >= 25 &&
                            asset.Kind is SupportAssetKind.TacticalUas or SupportAssetKind.MaleUas)
            .OrderBy(asset => asset.Role == AirMissionRole.BorderResponse ? 0 :
                              asset.Role == AirMissionRole.GroundReady ? 1 :
                              asset.Role == AirMissionRole.FrontlineIsr ? 3 : 2)
            .FirstOrDefault(asset => asset.Role != AirMissionRole.FrontlineIsr);

        if (selected is null)
        {
            selected = Assets
                .Where(asset => asset.Faction == TacticalFaction.Blue && asset.Available && asset.Armed && asset.FuelPercent >= 35 &&
                                asset.Kind == SupportAssetKind.FixedWingStrike && asset.Role == AirMissionRole.CombatAirPatrol)
                .OrderByDescending(asset => asset.FuelPercent)
                .FirstOrDefault();
        }

        if (selected is null)
        {
            mission.State = SupportMissionState.NoAssetAvailable;
            mission.Reason = "No dedicated armed response asset available; frontline ISR remains on station";
            return;
        }

        mission.Kind = selected.Kind == SupportAssetKind.FixedWingStrike
            ? SupportMissionKind.FixedWingStrike
            : SupportMissionKind.DroneStrike;
        AssignMission(state, mission, selected,
            busyTicks: selected.Kind == SupportAssetKind.FixedWingStrike ? 55 : 35,
            fuelCostPercent: selected.Kind == SupportAssetKind.FixedWingStrike ? 12 : 8,
            storeCost: 1,
            reason: selected.Kind == SupportAssetKind.FixedWingStrike
                ? "Airborne fixed-wing support assigned as fallback with abstract heavy guided store"
                : "Dedicated armed UAS response asset assigned; frontline ISR was not diverted");
    }

    public SupportImpactMarker RecordImpact(
        TacticalState state,
        SupportImpactKind kind,
        TacticalFaction faction,
        Position3D position,
        string label,
        string? sourceAssetId = null)
    {
        var marker = new SupportImpactMarker
        {
            Id = $"impact-{_nextImpact++:D5}",
            Kind = kind,
            Faction = faction,
            Position = position,
            CreatedTick = state.Tick,
            Label = label,
            SourceAssetId = sourceAssetId
        };
        Impacts.Add(marker);
        if (Impacts.Count > 160) Impacts.RemoveRange(0, Impacts.Count - 160);
        return marker;
    }

    public void Tick(TacticalState state)
    {
        foreach (var asset in Assets)
            UpdateAssetMotion(state, asset);

        foreach (var mission in Missions.Where(m => m.State == SupportMissionState.Executing &&
                     m.AssignedAssetId is not null).ToArray())
        {
            var asset = Assets.FirstOrDefault(a => a.Id == mission.AssignedAssetId);
            if (asset is null || asset.BusyUntilTick > state.Tick) continue;

            mission.State = SupportMissionState.Completed;
            mission.Reason = "Abstract support mission completed; exact attack geometry is intentionally not modeled";
            RecordImpact(state, ImpactKindFor(mission.Kind), mission.RequestingFaction, mission.EstimatedTarget,
                ImpactLabelFor(mission.Kind), asset.Id);
            asset.MissionTarget = null;
            asset.Available = true;
            asset.BusyUntilTick = null;
            asset.StatusText = HomeStatus(asset);
        }

        // Airborne fuel is deliberately synthetic and coarse; exact public performance figures are
        // not required for the simulation architecture.
        if (state.Tick > 0 && state.Tick % 60 == 0)
        {
            foreach (var asset in Assets.Where(asset => asset.IsAirborne))
            {
                var burn = asset.Kind == SupportAssetKind.FixedWingStrike ? 1.0 : 0.35;
                asset.FuelPercentExact = Math.Max(0, asset.FuelPercentExact - burn);
            }
        }

        Impacts.RemoveAll(marker => state.Tick - marker.CreatedTick > 240);
    }

    private static void AssignMission(
        TacticalState state,
        JointSupportMission mission,
        JointSupportAssetState selected,
        int busyTicks,
        double fuelCostPercent,
        int storeCost,
        string reason)
    {
        selected.Available = false;
        selected.StoresRemaining = Math.Max(0, selected.StoresRemaining - storeCost);
        selected.FuelPercentExact = Math.Max(0, selected.FuelPercentExact - fuelCostPercent);
        var transitTicks = selected.IsStatic ? 0 : EstimateTransitTicks(state, selected, mission.EstimatedTarget);
        selected.BusyUntilTick = state.Tick + busyTicks + transitTicks;
        selected.MissionTarget = selected.IsStatic ? null : mission.EstimatedTarget;
        selected.StatusText = $"Executing {mission.Id}";
        mission.AssignedAssetId = selected.Id;
        mission.State = SupportMissionState.Executing;
        mission.Reason = reason;
    }


    private static int EstimateTransitTicks(TacticalState state, JointSupportAssetState asset, Position3D target)
    {
        var distance = asset.Position.HorizontalDistanceTo(target);
        if (distance <= 1 || asset.MaxSpeedMetersPerSecond <= 0.01) return 0;

        // Coarse kinematic travel-time estimate. This is intentionally a simulation abstraction,
        // not a platform flight-performance table. It makes country-scale response time depend on
        // actual distance, current speed, max speed and acceleration.
        var accel = Math.Max(0.1, asset.AccelerationMetersPerSecondSquared);
        var vmax = Math.Max(1.0, asset.MaxSpeedMetersPerSecond);
        var v0 = Math.Clamp(asset.CurrentSpeedMetersPerSecond, 0, vmax);
        var accelSeconds = Math.Max(0, (vmax - v0) / accel);
        var accelDistance = (v0 + vmax) * 0.5 * accelSeconds;
        double travelSeconds;
        if (accelDistance >= distance)
        {
            var discriminant = Math.Max(0, v0 * v0 + 2 * accel * distance);
            travelSeconds = (-v0 + Math.Sqrt(discriminant)) / accel;
        }
        else
        {
            travelSeconds = accelSeconds + (distance - accelDistance) / vmax;
        }
        return Math.Max(1, (int)Math.Ceiling(travelSeconds / Math.Max(0.01, state.SecondsPerTick)));
    }

    private static void UpdateAssetMotion(TacticalState state, JointSupportAssetState asset)
    {
        if (asset.IsStatic)
        {
            asset.CurrentSpeedMetersPerSecond = 0;
            asset.CurrentAccelerationMetersPerSecondSquared = 0;
            return;
        }

        var dt = Math.Max(0.01, state.SecondsPerTick);
        if (!asset.IsAirborne && asset.MissionTarget is null)
        {
            var before = asset.CurrentSpeedMetersPerSecond;
            asset.CurrentSpeedMetersPerSecond = Math.Max(0,
                asset.CurrentSpeedMetersPerSecond - asset.DecelerationMetersPerSecondSquared * dt);
            asset.CurrentAccelerationMetersPerSecondSquared =
                (asset.CurrentSpeedMetersPerSecond - before) / dt;
            return;
        }

        Position3D target;
        if (asset.MissionTarget is { } missionTarget)
        {
            target = missionTarget;
        }
        else
        {
            var radius = Math.Max(500, asset.OrbitRadiusMeters);
            var cruise = Math.Max(1, asset.MaxSpeedMetersPerSecond);
            var angularRate = cruise / radius;
            var angle = asset.OrbitPhaseRadians + state.Tick * dt * angularRate;
            target = asset.OrbitCenter with
            {
                X = asset.OrbitCenter.X + Math.Cos(angle) * radius,
                Y = asset.OrbitCenter.Y + Math.Sin(angle) * radius
            };
        }

        var dx = target.X - asset.Position.X;
        var dy = target.Y - asset.Position.Y;
        var distance = Math.Sqrt(dx * dx + dy * dy);
        if (distance <= 1) return;

        var desired = asset.MaxSpeedMetersPerSecond;
        var braking = Math.Sqrt(Math.Max(0, 2 * asset.DecelerationMetersPerSecondSquared * distance));
        desired = Math.Min(desired, braking);
        var beforeSpeed = asset.CurrentSpeedMetersPerSecond;
        if (asset.CurrentSpeedMetersPerSecond < desired)
            asset.CurrentSpeedMetersPerSecond = Math.Min(desired,
                asset.CurrentSpeedMetersPerSecond + asset.AccelerationMetersPerSecondSquared * dt);
        else
            asset.CurrentSpeedMetersPerSecond = Math.Max(desired,
                asset.CurrentSpeedMetersPerSecond - asset.DecelerationMetersPerSecondSquared * dt);
        asset.CurrentAccelerationMetersPerSecondSquared =
            (asset.CurrentSpeedMetersPerSecond - beforeSpeed) / dt;

        var step = Math.Min(distance, asset.CurrentSpeedMetersPerSecond * dt);
        if (step <= 0) return;
        asset.Position = asset.Position with
        {
            X = asset.Position.X + dx / distance * step,
            Y = asset.Position.Y + dy / distance * step
        };
        asset.HeadingDegrees = Math.Atan2(dx, dy) * 180.0 / Math.PI;
        if (asset.HeadingDegrees < 0) asset.HeadingDegrees += 360;
    }

    private static string HomeStatus(JointSupportAssetState asset) => asset.Kind switch
    {
        SupportAssetKind.CounterBatteryRadar => "ACTIVE · watching for launch signatures",
        SupportAssetKind.TubeArtillery when asset.Faction == TacticalFaction.Blue => "READY · fire support",
        SupportAssetKind.RocketArtillery when asset.Faction == TacticalFaction.Red => "READY · rocket support",
        _ => asset.Role switch
        {
            AirMissionRole.FrontlineIsr => "AIRBORNE · frontline ISR",
            AirMissionRole.BorderResponse => "AIRBORNE · armed response orbit",
            AirMissionRole.CombatAirPatrol => "AIRBORNE · combat air patrol",
            AirMissionRole.GroundReady => "GROUND READY",
            AirMissionRole.StaticSupport => "READY · static support",
            _ => "Ready"
        }
    };

    private static SupportImpactKind ImpactKindFor(SupportMissionKind kind) => kind switch
    {
        SupportMissionKind.DroneStrike => SupportImpactKind.DroneStrike,
        SupportMissionKind.FixedWingStrike => SupportImpactKind.Airstrike,
        SupportMissionKind.RocketFire => SupportImpactKind.Rocket,
        SupportMissionKind.MortarFire => SupportImpactKind.Mortar,
        _ => SupportImpactKind.Artillery
    };

    private static string ImpactLabelFor(SupportMissionKind kind) => kind switch
    {
        SupportMissionKind.DroneStrike => "DRONE STRIKE",
        SupportMissionKind.FixedWingStrike => "AIRSTRIKE",
        SupportMissionKind.RocketFire => "ROCKET IMPACT",
        SupportMissionKind.MortarFire => "MORTAR IMPACT",
        _ => "ARTILLERY IMPACT"
    };

    private static double NextUnitInterval(DeterministicRng rng) =>
        rng.NextUInt32() / 4294967296.0;

    private static double EstimateFriendlyRisk(
        TacticalState state,
        JointSupportMission mission,
        IReadOnlyList<FriendlyTrackEstimate> friendlyTracks)
    {
        var score = 0.0;
        foreach (var friendly in friendlyTracks)
        {
            var uncertainty = friendly.UncertaintyAt(state.Tick, state.SecondsPerTick);
            var distance = friendly.LastKnownPosition.DistanceTo(mission.EstimatedTarget);
            var combined = mission.TargetUncertaintyRadiusMeters + uncertainty + 75;
            if (distance >= combined) continue;
            var overlap = 1.0 - Math.Clamp(distance / Math.Max(1, combined), 0, 1);
            score = Math.Max(score, overlap);
        }
        return score;
    }
}
