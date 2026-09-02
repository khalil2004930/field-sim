# v0.9 changelog

## Core

- Added `Position3D`, `Orientation3D`, `Bounds3D`.
- Added `TacticalWorldModel` with local meter coordinates and synthetic elevation.
- Added terrain/area/territory context, vegetation/building density, cover and concealment.
- Added `LineOfSightEngine` with clear/obscured/blocked results.
- Added visual/thermal/radar/acoustic signatures and sensor definitions.
- Added faction-specific `DetectionContact` and `FactionKnowledge`.
- Tactical stepping now updates detection after movement.

## Scenarios

- Village maps remain 13×13 but now instantiate a local 3D world at 120 m per cell.
- Synthetic elevation, built obstacles, agricultural/scrub/rocky terrain and territory bands are deterministic from the village seed.
- Village catalog now uses altered-name fictionalized labels such as `Aytaronn`, `Blidah`, `Maroun el-Rass`, `Kfarkilah`, etc.
- No real coordinate or real elevation source is attached to these village states.

## Organization

- Added a public peacetime IDF formation dataset with 217 nodes.
- Ground Forces / GOC is modeled as a force-building branch; Northern/Central/Southern regional commands remain parallel operational branches under the IDF root.
- Public sourced divisions, brigades and published battalions are represented where available.
- Live deployments, exact locations, readiness and current attachments are excluded.

## Vehicles

- Added data-driven vehicle definitions and runtime state hierarchy.
- Initial records: Merkava Mk.3B, Mk.3D, Mk.4M, Mk.4 Barak, Namer APC, Eitan APC.
- Added dimensions, crew, mobility, public nominal weapon capacity where sourced, optics description, synthetic protection zones, APS game state, component damage, reliability and normalized signatures.
- No speculative modern armor RHAe/mm or real RCS values are used.

## Desktop

- Updated branding to v0.9.
- Added Blue/Red perspective selector and faction-knowledge fog.
- Selected entity inspector now shows local XYZ, ground altitude, area, territory, nearest opponent, LOS and knowledge state.
- Added LOS ray overlay.
- Added IDF formation tree browser.
- Added vehicle browser with public nominal data and synthetic armor-zone indexes.

## CLI/tests

- Added `formations idf`, `vehicles idf`, and `spatial` runner commands.
- Added regression coverage for local XYZ, LOS blocking, asymmetric faction knowledge, formation validation and vehicle validation/runtime component state.
