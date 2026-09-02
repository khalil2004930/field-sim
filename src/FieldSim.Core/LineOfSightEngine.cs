namespace FieldSim.Core;

public static class LineOfSightEngine
{
    public static LineOfSightResult Evaluate(
        TacticalWorldModel world,
        Position3D observer,
        Position3D target,
        double sampleStepMeters = 10)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (sampleStepMeters <= 0) throw new ArgumentOutOfRangeException(nameof(sampleStepMeters));

        var horizontalDistance = observer.HorizontalDistanceTo(target);
        if (horizontalDistance < 0.001)
        {
            return new LineOfSightResult(
                LineOfSightState.Clear,
                horizontalDistance,
                double.PositiveInfinity,
                null,
                0,
                "Observer and target share the same horizontal position.");
        }

        var sampleCount = Math.Max(2, (int)Math.Ceiling(horizontalDistance / sampleStepMeters));
        var segmentLength = horizontalDistance / sampleCount;
        var minimumClearance = double.PositiveInfinity;
        var obscuration = 0.0;

        for (var index = 1; index < sampleCount; index++)
        {
            var fraction = index / (double)sampleCount;
            var x = Lerp(observer.X, target.X, fraction);
            var y = Lerp(observer.Y, target.Y, fraction);
            var rayZ = Lerp(observer.Z, target.Z, fraction);
            var context = world.ContextAt(x, y);
            var terrainZ = world.GroundAltitudeAt(x, y);
            var terrainClearance = rayZ - terrainZ;
            minimumClearance = Math.Min(minimumClearance, terrainClearance);

            if (terrainClearance <= 0)
            {
                return new LineOfSightResult(
                    LineOfSightState.Blocked,
                    horizontalDistance,
                    minimumClearance,
                    new Position3D(x, y, terrainZ),
                    Math.Clamp(obscuration, 0, 1),
                    "Terrain intersects the line of sight.");
            }

            if (context.BuildingDensity >= 0.45 && context.ObstacleHeightMeters > 0 &&
                rayZ <= terrainZ + context.ObstacleHeightMeters)
            {
                return new LineOfSightResult(
                    LineOfSightState.Blocked,
                    horizontalDistance,
                    minimumClearance,
                    new Position3D(x, y, terrainZ + context.ObstacleHeightMeters),
                    Math.Clamp(obscuration, 0, 1),
                    "A synthetic built obstacle intersects the line of sight.");
            }

            // Distance-integrated obscuration keeps the result approximately invariant when
            // sampleStepMeters changes. These are normalized game coefficients, not sensor equations.
            obscuration += context.VegetationDensity * segmentLength * 0.00225;
            obscuration += context.BuildingDensity * segmentLength * 0.00125;
        }

        obscuration = Math.Clamp(obscuration, 0, 1);
        var state = obscuration >= 0.22 ? LineOfSightState.Obscured : LineOfSightState.Clear;
        var reason = state == LineOfSightState.Clear
            ? "No terrain or synthetic obstacle blocks the ray."
            : "The ray is geometrically clear but passes through obscuring terrain.";

        return new LineOfSightResult(
            state,
            horizontalDistance,
            minimumClearance,
            null,
            obscuration,
            reason);
    }

    public static LineOfSightResult Evaluate(
        TacticalState state,
        Position3D observer,
        Position3D target,
        double sampleStepMeters = 10)
    {
        ArgumentNullException.ThrowIfNull(state);
        var terrainResult = Evaluate(state.World, observer, target, sampleStepMeters);
        if (terrainResult.State == LineOfSightState.Blocked) return terrainResult;

        var blocker = UrbanSpatialQueries.FirstSightBlocker(state, observer, target);
        if (blocker is null) return terrainResult;
        return new LineOfSightResult(
            LineOfSightState.Blocked,
            terrainResult.HorizontalDistanceMeters,
            terrainResult.MinimumClearanceMeters,
            blocker.Bounds.Center,
            terrainResult.ObscurationFactor,
            $"Synthetic structure '{blocker.Name}' intersects the line of sight.");
    }

    public static LineOfSightResult Evaluate(
        TacticalState state,
        TacticalUnit observer,
        TacticalUnit target)
    {
        var sensorHeight = observer.Sensors.Count > 0
            ? observer.Sensors.Max(sensor => sensor.MountHeightMeters)
            : observer.EyeHeightMeters;
        return Evaluate(state, observer, target, sensorHeight);
    }

    public static LineOfSightResult Evaluate(
        TacticalState state,
        TacticalUnit observer,
        TacticalUnit target,
        double observerMountHeightMeters)
    {
        state.Performance.RecordLineOfSight();
        var observerGround = state.GroundPositionOf(observer);
        var targetGround = state.GroundPositionOf(target);
        var from = observerGround with { Z = observerGround.Z + Math.Max(0.1, observerMountHeightMeters) };
        var to = targetGround with { Z = targetGround.Z + Math.Max(0.5, target.Bounds.HeightMeters * 0.55) };
        return Evaluate(state, from, to);
    }

    public static LineOfSightResult EvaluateCells(
        TacticalState state,
        GridPoint from,
        GridPoint to,
        double observerHeightMeters = 1.7,
        double targetHeightMeters = 1.7)
    {
        state.Performance.RecordLineOfSight();
        var observer = state.World.CellCenter(from, observerHeightMeters);
        var target = state.World.CellCenter(to, targetHeightMeters);
        return Evaluate(state, observer, target);
    }

    private static double Lerp(double first, double second, double fraction) =>
        first + (second - first) * fraction;
}
