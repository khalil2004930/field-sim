# v1.9.2 → v1.9.3 migration

## Scenario placement

v1.9.2 translated the old small village pattern around a hard-coded country-scale `ActiveAreaCenter`. This created geographically incoherent placements.

v1.9.3 removes that active path. The web session loads `v1_9_3_bint_jbeil_scenario.json`, resolves a public Bint Jbeil anchor through `ScenarioGeoProjection`, and derives all subordinate positions from synthetic named zones.

Legacy scenario packages remain loadable: `ScenarioInitialPlacement.xMeters/yMeters` is still supported when `zoneId` is absent.

## Objectives

`BattleObjective` now optionally carries `PrecisePositionMeters` and `CaptureRadiusMeters`. Legacy cell-based objectives continue to work. Country-scale objectives use meter-native control checks.

## Support assets

Support asset runtime behavior remains in `JointSupportModels`, but starting positions and orbit centers now come from scenario JSON through `ScenarioPlacementResolver` rather than hard-coded map coordinates.

## Safety/data boundary

Public settlement geography is only an anchor. Lower-level placements are intentionally fictionalized and synthetic.
