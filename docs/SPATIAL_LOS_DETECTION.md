# Spatial, LOS and detection model

## Position is intentionally small

`Position3D` contains local X/Y/Z only. It does not permanently store `forest`, `city`, `hostile territory`, etc. Those properties are derived from `TacticalWorldModel` so an entity cannot carry stale terrain metadata after moving.

```text
entity.Position3D
        │
        ├──> World.ContextAt(X,Y) ── terrain / area / territory / cover
        └──> World.GroundAltitudeAt(X,Y) ── local ground Z
```

World X increases east, world Y increases north, and Z is meters above a local synthetic datum. The 13×13 screen grid is converted into those local coordinates.

## LOS is not detection

The engine performs these concepts separately:

```text
field of view / range
        ↓
3D line of sight
        ↓
environmental obscuration
        ↓
sensor + target signature
        ↓
detection result
        ↓
classification / identification
        ↓
faction knowledge store
```

`LineOfSightEngine` samples the line between the sensor origin and target reference height. Terrain and synthetic obstacle height can block it. Vegetation/building density can leave it geometrically clear but obscured.

## Faction knowledge

There is no global `Spotted` boolean. `FactionKnowledge` answers what a particular faction currently knows about another entity. The same target can therefore be identified by Blue and completely unknown to Red.

A contact stores:

- observer unit;
- target unit;
- observing faction;
- last known local XYZ;
- last detection tick;
- classification;
- detection confidence;
- identification confidence.

The UI can hide this reciprocal information. Internally the simulation may know Red has detected Blue without revealing that fact to a Blue player.

## Signature model

The current visual/thermal/radar/acoustic values are normalized 0–1 **game signatures**. Radar is not square meters and is not a real radar-cross-section model. Detection strength, identification strength and sensor range are also simulation controls, not claims of real system performance.

## Future-safe extensions

The architecture can later support:

- directional signatures;
- day/night/weather modifiers;
- eye/sensor height by crew station;
- 3D building meshes or height fields;
- vegetation accumulation by path length;
- stale contact uncertainty ellipses;
- communications/datalink sharing between formations;
- sensor damage and degraded optics;
- thermal state and engine-on/off signature changes.
