# FieldSim v1.7 release notes

## Release theme

**Continuous world, derived formations, integrated equipment identity, and measurable simulation scaling.**

v1.7 is a foundation release. It deliberately changes the core spatial and scenario architecture before adding more content-heavy systems such as full CQB, projectile/material terminal effects, aircraft, engineering vehicles, delayed radio networks, or active multi-resolution aggregation.

## Major changes

### 1. Individuals are no longer 120 m chess pieces

Entity location is now represented by continuous local `Position3D` coordinates in meters. The remaining raster is terrain/navigation context, not the physical location of a person or vehicle.

The current prototype terrain cell size is 25 m while entity positions can differ by fractions of a meter. This keeps path planning practical while allowing later local navmeshes and structure interiors without changing the global coordinate model.

### 2. Formations do not move independently

ORBAT nodes have no independent physical simulation position. Their map geography is derived from the live entities assigned beneath them.

The derived spatial state includes mean, robust median position, dispersion, frontage, depth, and outlier count. This gives the web client a formation symbol location without creating a second fake movement system for platoons/companies/brigades.

### 3. Spatial broad phase

`SpatialHashIndex` replaces repeated whole-world scans for common nearby-entity queries. The bucket size is an implementation detail only and does not quantize entity positions.

### 4. Better navigation foundation

A* now uses `PriorityQueue` for its open set. Movement speed is expressed in meters per second and differs by entity class. High-level formation orders distribute nearby exact destinations to subordinate entities instead of stacking all members on one destination point.

### 5. Detection/LOS hardening

Detection candidate selection uses the spatial broad phase. Sensor field of view and orientation are now considered, sensor mount height is used for observer origin, and LOS obscuration is integrated by distance rather than being a direct artifact of sample count.

### 6. Stable scenario identity

`EntityKey` is the stable authored identity while numeric `TacticalUnit.Id` remains a runtime identifier. `ScenarioPackage` owns ORBAT/equipment/placement assignments and replaces the old hardcoded runtime-ID to ORBAT dictionary.

### 7. Hezbollah small-arms OSINT integration

The 18-record v1.0 public-source small-arms dataset is included with provenance and evidence grades. Selected Red demo entities reference data-backed weapon identities. Their allocation in the scenario is explicitly synthetic and does not claim real issue scale.

Weapon identity remains separate from cartridge/projectile/optic/magazine/under-barrel-weapon identity so later terminal-effects work does not corrupt the source-data model.

### 8. Ammunition identity layer

Six cartridge-family identity records are included as the first ammunition layer. They intentionally do not contain detailed terminal-effect or armor-penetration coefficients yet.

### 9. AI intent foundation

`AiIntent` introduces the intended hierarchy of faction -> formation -> subordinate -> entity goal translation. Upper layers express desired state; local entities execute movement/action. Formations still do not gain physical positions.

### 10. CQB/underground contracts

Structure-volume contracts now support buildings, levels including negative/underground levels, compartments, portals, stairs/hatches/tunnel links, and local navigation attachments. This is a data/model foundation only; v1.7 does not claim a complete CQB solver.

### 11. Simulation LOD contract

The release introduces detail-level contracts for future aggregate formation, formation, entity, and close-quarters simulation. Automatic expand/collapse is intentionally deferred until entity-state reconciliation and replay are stronger.

### 12. Replay/AAR backbone

`SimulationJournal` adds monotonically sequenced semantic events. This is the future source for delta streaming, replay bookmarks, scrubbing, and AAR rather than relying only on UI text logs.

### 13. Web scaling

The web server caches snapshots by simulation revision. The SSE stream sends a snapshot only when revision changes and otherwise emits keepalive comments. ORBAT aggregation is calculated in batches, and close-zoom individuals/trails are rendered through MapLibre GeoJSON layers instead of one DOM marker per person.

### 14. Measurable performance

Per-tick counters expose spatial queries/candidates, LOS evaluations, detection observers/candidate pairs, path searches, and A* expansions so future optimization can be based on measurements rather than guesses.

## Intentionally not finished in v1.7

- full room-by-room CQB navigation and cover reasoning;
- underground LOS/projectile interaction;
- detailed vehicle component combat integrated into the infantry engagement loop;
- delayed/lossy C2 knowledge propagation;
- detailed projectile, body-armor, wall, bunker, and structural terminal effects;
- active aggregate/expand LOD;
- aircraft/UAV/UCAV flight and combat layers beyond existing foundation models;
- full logistics network and maintenance flow;
- full web delta protocol after each simulation revision.

These belong on top of the v1.7 world/entity foundation rather than being bolted onto the old coarse-cell model.

## Validation boundary

Static validation checks JSON, JS syntax, XML/XAML/project structure, scenario references, ORBAT tree integrity, stable entity assignments, weapon references, source invariants, and clean-room rules. The release environment does not contain the .NET SDK, so `build_windows.bat` remains the authoritative compiler and executable-test pass on a .NET 10 machine.
