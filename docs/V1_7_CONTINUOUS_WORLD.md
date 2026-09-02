# FieldSim v1.7 — Continuous World Core

## Purpose

v1.7 is the architectural break from the early 120 m-per-cell individual simulation. Individual entities now have continuous local XYZ positions measured in meters. Coarse cells remain only for terrain lookup, broad path planning, scenario references, and acceleration.

This is deliberately a hybrid spatial model:

```text
continuous XYZ entity positions
        |
        +-- spatial hash broad phase
        +-- coarse terrain/navigation grid
        +-- formation spatial aggregation
        +-- future local building/navmesh detail
        +-- future aggregate/expand simulation LOD
```

A 1 m global grid is intentionally avoided. A 20 km × 20 km one-meter raster would contain 400 million cells before adding height, occupancy, materials, navigation, buildings, underground spaces, hazards, or other layers.

## Entity position

`TacticalUnit.PrecisePositionMeters` is authoritative when present. `TacticalUnit.Position` remains a coarse `GridPoint` synchronized from the exact position.

Movement advances in meters per simulation tick toward continuous waypoints. Multiple infantry entities can occupy different coordinates inside the same coarse terrain cell.

## Formation position

ORBAT formations are not physical chess pieces. `FormationSpatialCalculator` derives a live formation spatial state from the subordinate entities assigned to the branch:

- mean position;
- robust median position used as the map centroid;
- 90th-percentile dispersion radius;
- frontage and depth;
- entity count;
- outlier count.

An isolated scout therefore does not drag the displayed formation symbol as aggressively as a simple arithmetic mean would.

## High-level orders without a moving formation object

A web ORBAT order targets a branch. The server resolves the currently linked live entities and creates nearby deterministic exact destinations around the requested objective. Only those entities move; the formation's displayed position follows from their resulting spatial distribution.

The allocator is a synthetic game-system spread, not a real-world formation/tactics model.

## Spatial performance

`SpatialHashIndex` is the first broad-phase accelerator. It reduces nearby-unit and sensor candidate queries from repeated whole-world scans to bucketed local queries while preserving continuous positions.

Pathfinding still uses the coarse navigation raster, but the A* open set now uses `PriorityQueue` instead of scanning the entire open set for the cheapest node.

Detection performs a spatial range query first, then checks per-sensor FOV, environment, LOS, and normalized detection probability only for candidates inside the relevant area.

## LOS change

v1.7 integrates vegetation/building obscuration by traveled segment length instead of adding a fixed amount per sample. Changing the LOS sample interval should therefore no longer materially change the obscuration result for the same synthetic terrain.

Sensor LOS uses each sensor's mount height rather than one maximum observer sensor height for all channels.

## Performance counters

`SimulationPerformanceCounters` records per-tick:

- spatial radius queries;
- spatial candidates examined;
- LOS evaluations;
- detection observer scans;
- detection candidate pairs;
- path searches;
- A* nodes expanded.

These counters never affect simulation results. The web Status panel exposes them so scaling regressions can be seen while scenarios grow.

## Future CQB / buildings / underground

v1.7 adds the data contract, not a full CQB solver:

- `StructureVolume`;
- `StructureCompartment`;
- 3D axis-aligned bounds;
- floors via `LevelIndex`;
- underground negative/local Z support;
- `NavigationPortal` for doors, openings, stairs, ladders, ramps, hatches and tunnel connections;
- broad material classes.

The intended future architecture is world spatial index -> nearby structure -> building-local navigation/geometry. FieldSim should not become a giant global 3D voxel grid.

## Simulation LOD contract

`SimulationDetailLevel` introduces four levels:

1. `AggregateFormation`
2. `Formation`
3. `Entity`
4. `CloseQuarters`

v1.7 does not yet collapse and re-expand formations. It establishes the registry/policy contract so later versions can do so without changing stable entity/formation identity.

## Known limitations

- Infantry AI still contains coarse-cell cover/firing-position searches; those are planning LOD, not final close-quarters navigation.
- Faction knowledge is still updated immediately at faction scope; communications/C2-delayed information propagation is not yet connected.
- The current infantry engagement engine still primarily resolves soldier targets; vehicle-vs-infantry and full component damage integration remain later work.
- Structure volumes are not yet used for projectile collision, cover, pathfinding, or room-level AI.
- LOD aggregation/expansion is a contract only in v1.7.


## Scenario-authored placement

A scenario can optionally author an exact synthetic starting X/Y position and heading for a stable entity key. The runtime derives Z from the local terrain provider and validates that the position is inside the local world. This is intentionally entity-level state. ORBAT nodes continue to derive their location and dispersion from their live descendants.
