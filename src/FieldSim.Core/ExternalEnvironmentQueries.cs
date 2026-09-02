namespace FieldSim.Core;

/// <summary>
/// Optional boundary for delegating physical-world queries to an external environment engine.
/// Returning false means "not handled" and causes FieldSim to fall back to its built-in
/// synthetic world model.
/// </summary>
public interface IExternalEnvironmentQueryProvider
{
    bool TryGetGroundAltitude(double xMeters, double yMeters, out double altitudeMeters);

    bool TryEvaluateLineOfSight(
        Position3D observer,
        Position3D target,
        out LineOfSightResult result);

    bool TryIsPointInsideStructure(Position3D point, out bool insideStructure);

    bool TryIsWalkable(
        Position3D point,
        TacticalUnitClass unitClass,
        out bool walkable);

    bool TryIsMovementSegmentBlocked(
        Position3D from,
        Position3D to,
        TacticalUnitClass unitClass,
        out bool blocked);

    bool TryFindPath(
        Position3D from,
        Position3D to,
        TacticalUnitClass unitClass,
        out IReadOnlyList<Position3D> waypoints);
}
