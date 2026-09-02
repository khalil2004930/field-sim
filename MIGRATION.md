# FieldSim -> Unity 6 migration plan

## Phase 1 - Environment foundation (this package)

- True XYZ world coordinates.
- Mountain terrain.
- Buildings/bunkers as physical structures.
- Damageable structural parts and persistent collapse state.
- Tunnel graph and portals.
- LOS through Unity Physics.
- Ground NavMesh foundation.
- Physical FPV/support assets.

## Phase 2 - Bring in the existing simulation core

Create a Unity-independent C# assembly named `FieldSim.SimCore` from the current repository. Remove renderer/UI dependencies from the simulation classes. Preserve existing scenario IDs, ORBAT IDs, support-asset IDs, random seed, C2 reports, support missions, and diagnostic event formats where practical.

Unity must not become authoritative for simulation decisions. The bridge should be one-way per simulation tick:

1. SimCore advances one fixed simulation step.
2. SimCore publishes an immutable world snapshot plus events.
3. Unity applies positions/states to GameObjects and answers requested environment queries.
4. Query results are returned to SimCore for the next tick.

## Phase 3 - Environment behavior

- Building entry and exit through portals.
- Building occupancy and clearance state.
- Dynamic NavMesh updates after wall/entrance collapse.
- Surface-to-underground traversal.
- Terrain/building LOS and occlusion.
- Road and rubble state.

## Phase 4 - Combat rewrite

Do not port the old combat loop unchanged. Build the new resolver only after the environment tests pass. Keep all weapon-effect coefficients synthetic/game-calibrated.

## Definition of done for Phase 1

- No ground agent can walk through a wall collider.
- Terrain and structures block LOS consistently.
- Buildings at different elevations sit on the terrain correctly.
- A wall can be damaged/collapsed without deleting the whole building.
- One synthetic area effect can damage multiple nearby structures.
- A collapsed portal disables traversal.
- Tunnel edge removal can split the underground network.
- The mortar is a selectable physical world entity.
- An FPV can leave Stowed state and visibly move in XYZ space.
