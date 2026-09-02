# FieldSim v1.9.3 — Scenario Placement Repair

## Scope

This release is intentionally limited to **Step 1** from the v1.9.2 gameplay review: repair country-scale scenario geography, initial placement, support placement, and meter-native objective coordinates before changing the tactical AI.

## Root cause removed

v1.9.2 carried the old ~325 m village pattern into the country-scale world by subtracting an old pattern center and translating everything around a hard-coded `ActiveAreaCenter`. That made the country map look authoritative while the active battle could sit tens of kilometers away from the intended public-place reference.

v1.9.3 removes that active placement path.

## New scenario authoring chain

```text
public-place GeoAnchor
        ↓
synthetic PlacementZone
        ↓
entity / objective / support offset
        ↓
continuous local XYZ meters
        ↓
MapLibre display projection
```

The current primary public-place anchor is Bint Jbeil. It projects to approximately `X 41.45 km / Y 23.63 km` under the existing country-scale display projection.

## New models

- `ScenarioGeoAnchor`
- `ScenarioPlacementZone`
- `ScenarioObjectiveDefinition`
- `ScenarioSupportPlacement`
- `ScenarioGeoProjection`
- `ScenarioPlacementResolver`

Legacy absolute `ScenarioInitialPlacement.xMeters/yMeters` remains supported. v1.9.3 scenario authoring prefers `zoneId + offsetEastMeters + offsetNorthMeters`.

## Ground-force placement

The 14 current integration entities are still a tiny prototype force; this release does not pretend otherwise. Their placement is now coherent:

- Red runtime entities are placed in synthetic central/west/east Bint Jbeil defense zones or a local rear zone.
- Blue runtime entities are placed in synthetic southern approach zones.
- Every entity placement is authored in JSON and checked at runtime against its zone.

## Support placement

The following support assets now resolve through scenario JSON rather than magic map coordinates:

- counter-battery radar
- 2 × M109 support elements
- 5 × Hermes 450 states
- 6 × F-16I states
- Red mortar support
- Red 107 mm rocket support
- Red BM-21 / 122 mm support

Named support-sector labels such as Dishon/Odeiseh remain **scenario labels with synthetic offsets**. They are not exact real firing positions.

## Objectives

Country-scale `BattleObjective` now supports:

- `PrecisePositionMeters`
- `CaptureRadiusMeters`

The old cell-based objective mode remains for legacy tactical scenarios. The Bint Jbeil scenario uses meter-native objective radii so a `1 km` compatibility cell can no longer silently become the objective capture radius.

## Placement validation

Scenario loading now checks:

- duplicate geo-anchor / zone / support IDs;
- missing anchor/zone references;
- non-finite or out-of-theater positions;
- entity/support offsets outside their authored zone radius;
- wrong-faction entity/support assignment to a faction-labelled zone;
- runtime entity position mismatch against authored placement;
- runtime support position mismatch against authored placement;
- presence of the primary Bint Jbeil anchor inside the theater.

The intention is to fail loudly rather than silently display a unit in the wrong country/sector.

## Browser changes

- A visible `BINT JBEIL · SCENARIO ANCHOR` marker makes the scenario reference obvious.
- `Fit scenario` now fits active units, support assets, objectives, and the anchor instead of zooming to the entire 160 × 210 km theater.

## Data boundary

Bint Jbeil is a public geographic reference. All subordinate combat zones, individual positions, support sectors, artillery locations, radar positions, UAV orbits, aircraft patrol/staging positions and event geometry are fictionalized/synthetic scenario data and are not claims about current real deployments.

## Next repair

Step 2 should remove kilometer-cell thinking from tactical AI decisions (`CellsWithin`, cover search, firing-position search, local cohesion/medic logic) and replace those paths with meter-native spatial-index queries.
