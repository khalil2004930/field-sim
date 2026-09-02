# FieldSim v1.9.2 — Country-Scale Continuous World

## Purpose

v1.9.2 removes the old visible tactical-grid workflow from the primary web client and moves the playable sandbox onto a country-scale continuous XYZ coordinate frame. Entities have exact meter positions; formation symbols remain derived from their actual members.

## Spatial model

- Theater frame: 160 km × 210 km continuous local XYZ meters.
- Browser orders can target any point in the theater at 0.5 m display/input precision.
- Soldiers and vehicles physically move toward exact continuous destinations.
- When a straight route is clear, movement no longer walks through planning-cell centers.
- A coarse 1 km compatibility lattice remains internal for legacy terrain/path detours only. It is not rendered, is not a soldier step size, and is not the authoritative position model.
- Map display projects the synthetic local world over a public OpenStreetMap basemap for context only.

The current country terrain is synthetic/coarse and is not a real elevation/obstacle database. Building/CQB navigation can later use local meshes/graphs without changing entity coordinates.

## Mobility

Every live tactical entity exposes:

- current speed (m/s);
- current acceleration (m/s²);
- maximum speed (m/s);
- acceleration capability (m/s²);
- deceleration capability (m/s²).

Movement integrates speed and acceleration each simulation tick and brakes into exact destinations. Infantry, light vehicles, APCs, armored vehicles and tanks use different mobility profiles.

Joint-support aircraft use the same state concept. Their current position changes every tick:

- Hermes-class scenario UAVs are substantially faster than ground units;
- F-16I scenario aircraft are substantially faster than UAVs;
- support response time now includes a coarse distance/speed/acceleration transit estimate rather than completing after a fixed timer independent of range.

All aircraft mobility figures in this sandbox are normalized simulation values, not a flight-manual performance model.

## Visible joint support

The map now directly renders runtime markers for:

### Blue

- counter-battery radar in a synthetic Israeli-side rear sector;
- M109 Doher support element labelled `Dishon sector · synthetic offset`;
- M109 Doher support element labelled `Odeiseh sector · synthetic offset`;
- five Hermes 450 scenario aircraft;
- six F-16I scenario aircraft, including two moving CAP aircraft.

### Red

- forward mortar support section;
- intermediate 107 mm rocket-support element;
- deeper-south BM-21 / 9M22-class rocket-support element.

Named sectors are deliberately coarse labels backed by synthetic offsets. They are not exact current military emplacements.

## Impact layer

Joint support now leaves temporary map events:

- `✸` — fixed-wing airstrike;
- `◆` — UAV strike;
- `✹` — tube-artillery impact;
- `▲` — rocket impact;
- `●` — mortar impact.

The impact marker is a simulation event marker, not a real-world targeting coordinate.

## Counter-battery / support chain

A Red rocket-support event can produce an intentionally imprecise counter-battery cue. The Blue support allocator screens friendly-position uncertainty and preserves frontline ISR where possible. Dedicated response UAV support is preferred; fixed-wing CAP remains a fallback.

The model deliberately avoids real radar error tables, real attack profiles, real release parameters and exact current deployment data.

## Performance direction

The country-scale frame does **not** allocate a 1 m raster across the country. Continuous coordinates are paired with spatial hashing and coarse planning context. This permits sub-meter entity positions without hundreds of millions of world cells.

Future optimization should continue toward:

- local navmesh/room graphs for CQB;
- road graphs and cost fields for vehicles;
- local high-fidelity terrain chunks;
- simulation LOD for distant formations;
- aggregate distant fire/combat resolution;
- incremental state streaming to the web client.
