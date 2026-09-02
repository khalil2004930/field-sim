namespace FieldSim.Core;

public static class DetectionEngine
{
    public static void Update(TacticalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.EnsurePrecisePositions();

        foreach (var observer in state.Units.Where(unit => unit.Alive && unit.Sensors.Count > 0 && unit.Soldier?.IsEvacuated != true))
        {
            state.Performance.RecordDetectionObserver();
            var observerGround = state.GroundPositionOf(observer);
            var maxRange = observer.Sensors.Max(sensor => sensor.Clamp().MaximumRangeMeters) * 1.10;
            var candidates = state.SpatialIndex.QueryRadius(observerGround, maxRange);
            state.Performance.RecordSpatialQuery(candidates.Count);
            state.Performance.RecordDetectionCandidates(candidates.Count);
            foreach (var targetId in candidates)
            {
                if (targetId == observer.Id) continue;
                var target = state.UnitById(targetId);
                if (target is null || !target.Alive || target.Soldier?.IsEvacuated == true || target.Faction == observer.Faction) continue;
                TryDetect(state, observer, target);
            }
        }
    }

    private static void TryDetect(
        TacticalState state,
        TacticalUnit observer,
        TacticalUnit target)
    {
        var observerGround = state.GroundPositionOf(observer);
        var targetGround = state.GroundPositionOf(target);
        var distance = observerGround.HorizontalDistanceTo(targetGround);
        var context = state.ContextOf(target);
        var bestDetectionScore = 0.0;
        var bestIdentificationScore = 0.0;

        foreach (var rawSensor in observer.Sensors)
        {
            var sensor = rawSensor.Clamp();
            if (!InsideFieldOfView(observer, observerGround, targetGround, sensor.FieldOfViewDegrees)) continue;

            var environmentModifier = EnvironmentModifier(state, observer, sensor.Type);
            var effectiveRange = Math.Max(1, sensor.MaximumRangeMeters * environmentModifier.RangeMultiplier);
            if (distance > effectiveRange) continue;

            var los = LineOfSightEngine.Evaluate(state, observer, target, sensor.MountHeightMeters);
            if (los.State == LineOfSightState.Blocked) continue;

            var rangeFraction = distance / effectiveRange;
            var signature = target.Signature.For(sensor.Type);
            var score = sensor.DetectionStrength * 0.55 + signature * 0.45;
            score *= environmentModifier.StrengthMultiplier;
            score -= rangeFraction * 0.42;
            score -= context.Concealment * 0.28;
            score -= los.ObscurationFactor * 0.34;
            if (target.Path.Count > 0 || target.ContinuousWaypoints.Count > 0 || target.MovementDestinationMeters is not null) score += 0.08;
            score = Math.Clamp(score, 0, 1);

            var identification = (sensor.IdentificationStrength * 0.58 + signature * 0.22) *
                                 environmentModifier.IdentificationMultiplier;
            identification -= rangeFraction * 0.34;
            identification -= context.Concealment * 0.18;
            identification -= los.ObscurationFactor * 0.24;
            identification = Math.Clamp(identification, 0, 1);

            bestDetectionScore = Math.Max(bestDetectionScore, score);
            bestIdentificationScore = Math.Max(bestIdentificationScore, identification);
        }

        if (bestDetectionScore <= 0) return;

        // Deterministic simulation probability, intentionally not a real sensor equation.
        var roll = state.Random.NextInclusive(0, 10_000) / 10_000.0;
        var detected = bestDetectionScore >= 0.78 || roll <= bestDetectionScore * 0.55;
        if (!detected) return;

        var classification = Classify(target, bestIdentificationScore);
        var targetPosition = targetGround with { Z = targetGround.Z + Math.Max(0.5, target.Bounds.HeightMeters * 0.5) };
        state.CommandAndControl.RegisterDetection(
            state, observer, target, targetPosition, classification, bestDetectionScore, bestIdentificationScore);
    }

    private static bool InsideFieldOfView(
        TacticalUnit observer,
        Position3D observerPosition,
        Position3D targetPosition,
        double fieldOfViewDegrees)
    {
        if (fieldOfViewDegrees >= 359.9) return true;
        var dx = targetPosition.X - observerPosition.X;
        var dy = targetPosition.Y - observerPosition.Y;
        if (Math.Abs(dx) < 0.001 && Math.Abs(dy) < 0.001) return true;

        // World Y points north, X east. Heading zero is north and increases clockwise.
        var bearing = Math.Atan2(dx, dy) * 180.0 / Math.PI;
        if (bearing < 0) bearing += 360;
        var heading = NormalizeDegrees(observer.Orientation.HeadingDegrees);
        var delta = Math.Abs(NormalizeSignedDegrees(bearing - heading));
        return delta <= fieldOfViewDegrees * 0.5;
    }

    private static double NormalizeDegrees(double degrees)
    {
        var normalized = degrees % 360;
        return normalized < 0 ? normalized + 360 : normalized;
    }

    private static double NormalizeSignedDegrees(double degrees)
    {
        var normalized = NormalizeDegrees(degrees);
        return normalized > 180 ? normalized - 360 : normalized;
    }

    private static (double RangeMultiplier, double StrengthMultiplier, double IdentificationMultiplier) EnvironmentModifier(
        TacticalState state, TacticalUnit observer, SensorType type)
    {
        var environment = state.Environment;
        var weatherRange = Math.Clamp(environment.VisibilityMeters / 4000.0, 0.12, 1.0);
        var optic = observer.Soldier?.PrimaryWeapon.Optic;

        return type switch
        {
            SensorType.Visual when optic?.Thermal == true =>
                (0.82 * weatherRange, 0.82, 0.78),
            SensorType.Visual when optic?.NightVision == true =>
                (Math.Clamp((0.48 + Math.Sqrt(Math.Max(0.02, environment.AmbientLight01)) * 0.62) * weatherRange, 0.18, 1.0),
                 Math.Clamp(0.55 + Math.Sqrt(Math.Max(0.02, environment.AmbientLight01)) * 0.55, 0.28, 1.0),
                 Math.Clamp(0.50 + Math.Sqrt(Math.Max(0.02, environment.AmbientLight01)) * 0.48, 0.24, 1.0)),
            SensorType.Visual =>
                (Math.Clamp((0.18 + environment.AmbientLight01 * 0.82) * weatherRange, 0.08, 1.0),
                 Math.Clamp(0.22 + environment.AmbientLight01 * 0.78, 0.16, 1.0),
                 Math.Clamp(0.16 + environment.AmbientLight01 * 0.84, 0.12, 1.0)),
            SensorType.Thermal =>
                (Math.Clamp((0.62 + environment.ThermalContrast01 * 0.45) *
                    (environment.Visibility is WeatherVisibility.Fog or WeatherVisibility.Rain ? 0.74 : 1.0), 0.25, 1.08),
                 Math.Clamp(0.58 + environment.ThermalContrast01 * 0.50, 0.35, 1.08),
                 Math.Clamp(0.45 + environment.ThermalContrast01 * 0.42, 0.30, 0.95)),
            SensorType.Acoustic =>
                (Math.Clamp(1.0 - Math.Min(0.45, environment.WindMetersPerSecond * 0.025), 0.45, 1.0), 1.0, 0.55),
            _ => (1.0, 1.0, 1.0)
        };
    }

    private static ContactClassification Classify(TacticalUnit target, double confidence)
    {
        if (confidence >= 0.82) return ContactClassification.Identified;
        if (target.UnitClass == TacticalUnitClass.AirObject && confidence >= 0.58)
            return ContactClassification.AirObject;
        if ((target.UnitClass is TacticalUnitClass.ArmoredVehicle or TacticalUnitClass.Tank or TacticalUnitClass.Apc) &&
            confidence >= 0.60)
            return ContactClassification.ArmoredVehicle;
        if ((target.UnitClass is TacticalUnitClass.Vehicle or TacticalUnitClass.ArmoredVehicle or
            TacticalUnitClass.Tank or TacticalUnitClass.Apc) && confidence >= 0.42)
            return ContactClassification.Vehicle;
        if (target.UnitClass == TacticalUnitClass.Person && confidence >= 0.42)
            return ContactClassification.Person;
        return confidence >= 0.28 ? ContactClassification.Contact : ContactClassification.Unknown;
    }
}
