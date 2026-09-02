# v1.9.1 → v1.9.2 migration

- Web runtime loads `v1_9_2_country_scale_scenario.json`.
- `CountryScaleScenario` replaces the old small active map for the web runtime.
- Exact continuous-meter order targets are primary; old integer grid fields remain compatibility-only API fields.
- `MobilityProfile` supplies max speed, acceleration and deceleration to tactical entities.
- Joint-support assets expose live XYZ/mobility state and move while airborne/on a mission.
- Support impacts are first-class snapshot events and map symbols.
- Do not build new gameplay features against `GridPoint` as an authoritative physical coordinate. Use `Position3D`/continuous meter positions.
