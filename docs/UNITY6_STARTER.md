# FieldSim Unity 6 migration starter

This is the first migration slice for FieldSim. It intentionally focuses on the environment layer before combat.

## What is included

- Unity 6 project skeleton.
- Meter-native FieldSim XYZ to Unity coordinate conversion with floating-origin recentering.
- Terrain-aware line-of-sight queries using Unity Physics.
- Destructible building and bunker structure model with local structural parts.
- Persistent damage states: intact, damaged, heavy damage, critical, collapsed.
- Area structural effects that can damage more than one nearby structure using synthetic normalized effect values.
- Portals for doors, bunker entrances, basements, tunnel entrances, and surface exits.
- Underground tunnel connectivity graph with blockable segments.
- Occupancy tracking for soldiers and other world entities.
- Physical support-asset entity model so mortars, launchers, and other support assets can exist at real world coordinates.
- FPV lifecycle and actual XYZ motion: Stowed -> Launching -> Airborne -> Holding/Landed/Expended.
- Legacy diagnostic JSON importer for `snapshot.units` and `snapshot.jointSupportAssets`.
- A Unity Editor menu that creates a synthetic mountainous environment prototype with buildings, a bunker, a tunnel graph, a mortar, soldier dots, and a moving FPV.

## Important boundary

The source repository could not be fetched from this execution environment, so this package does not yet move the existing FieldSim classes. It is structured to be added under `unity/FieldSim.Unity` in the existing repository. Once repository access is available, the next migration step is to extract the current pure C# simulation state into a Unity-independent `FieldSim.SimCore` assembly and bind it to these Unity world entities.

## Open in Unity

1. Install Unity 6.0 LTS through Unity Hub.
2. Open `unity/FieldSim.Unity` as a project.
3. Let Unity restore `com.unity.ai.navigation@2.0.9`.
4. In the Editor choose `FieldSim > Prototype > Create Mountain Environment Scene`.
5. Open `Assets/Scenes/EnvironmentPrototype.unity` if Unity does not open it automatically.
6. Press Play.

The prototype FPV launches automatically and follows a 3D waypoint route. Press `Space` in Play mode to apply a synthetic structural impact to a cluster of buildings and observe local/nearby structure damage.

## Migration rule

Unity owns geometry, physics, rendering, terrain, navigation, building/tunnel topology, and world-space queries. FieldSim remains authoritative for scenario time, AI decisions, C2/ISR, unit state, combat state, artillery missions, morale, logistics, and deterministic RNG.
