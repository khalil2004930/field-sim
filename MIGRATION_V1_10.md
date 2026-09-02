# v1.9.3 → v1.10 migration

## Scenario package

The web session now loads `data/scenarios/v1_10_urban_c2_scenario.json`. It keeps the v1.9.3 geo-anchor/zone placement model and adds v1.10 gameplay systems on top of it.

## Knowledge / C2

`DetectionEngine` no longer writes detections directly into faction-wide knowledge. It calls `CommandAndControlState.RegisterDetection`:

1. the observing unit receives immediate local knowledge;
2. an abstract contact report is queued;
3. after a normalized delay (and possible abstract loss), the report enters the shared faction picture.

Code that needs the information available to one entity should use `CommandAndControlState.ContactsKnownBy` / `Knows`, not only `TacticalState.Knowledge`.

## Urban geometry

`TacticalState.Structures` is populated with deterministic synthetic structures in the Bint Jbeil-centered scenario. `UrbanSpatialQueries` provides meter-native building footprint checks, cover scoring, movement blockers and simple exterior detours. Exact real building footprints are intentionally not imported.

## Tactical AI

Local cover/firing-position/regroup searches now use meter distances. The hidden country-scale lattice remains a compatibility/planning layer but should not be used as the local tactical distance unit.

## Objective state

`BattleObjective` now tracks side-specific `ObjectiveProgressState` values. Control is not permanent merely because a side once crossed the threshold; abandoned control decays and can become `Lost`.

## Casualties / sustainment

`SoldierRuntime` adds casualty disposition, evacuation request linkage, evacuation status and supply readiness. `CasualtyLogisticsEngine` updates these states and may create abstract support requests.

## Operational support

`OperationalSupportState` now faction-binds runtime assets and supports same-side assignment for abstract requests. This is separate from the existing visible `JointSupportState` flight/fires demonstration layer; later versions can converge the two.

## Diagnostics

`GET /api/diagnostics/report` returns `fieldsim-diagnostic-v1`. The web `REPORT / AAR` button enriches the server report with browser errors/request history and supports copy/download.
