# FieldSim v1.4 - ORBAT Command Center

v1.4 converts the ORBAT prototype into the primary battlefield interface.

## Main layout

- Permanent ORBAT pane on the left.
- Battlespace in the center.
- Formation and selected-unit context on the right.
- Live event/AAR stream across the bottom.
- The old Observer workflow is no longer the primary screen.

## ORBAT command workflow

Blue remains NATO-style down to platoon in Tactical depth. Red remains irregular and may expand into squads, teams, cells and support elements.

Selecting an ORBAT node shows its scenario status and the live tactical entities bound below that branch. Selecting a tactical entity on the map also updates the formation context to its bound ORBAT node.

The formation pane now exposes prototype high-level commands:

- Hold
- Advance
- Defend
- Support
- Withdraw

Advance, Defend, Support and Withdraw use the selected local tactical grid reference as the scenario objective. These commands are software/game abstractions and are not current real-world procedures.

## 3D bridge view

The new `3D LOCAL VIEW` uses WPF `Viewport3D` to render:

- the same local synthetic terrain elevations used by FieldSim LOS;
- terrain-class coloring;
- live Blue/Red entity positions;
- selected-unit highlighting.

The 3D view is intentionally a bridge/prototype. FieldSim.Core remains authoritative. A future Unity client can consume the same state rather than moving simulation authority into rendering/physics.

## Data boundary

The scenario ORBAT and local terrain remain synthetic task-organization and game-space data. The separate public reference databases are not treated as live deployments or precise scenario dispositions.
