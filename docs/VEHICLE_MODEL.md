# Ground vehicle model

## Why definitions are data-driven

The code uses broad runtime classes (`TankEntityState`, `ApcEntityState`) and loads variants as data. This prevents duplicated logic across Merkava variants and lets future Namer/Eitan variants share the same damage, fuel, sensor and protection systems.

```text
SimEntityState
└── VehicleEntityState
    └── GroundVehicleEntityState
        └── ArmoredVehicleEntityState
            ├── TankEntityState
            └── ApcEntityState
```

## Initial vehicle dataset

- Merkava Mk.3B
- Merkava Mk.3D / Dor Dalet
- Merkava Mk.4M
- Merkava Mk.4 Barak
- Namer heavy APC
- Eitan 8×8 APC

The dataset cites public Wikipedia, IDF, Rafael and Elbit sources. Values prefixed/described as `Public...` are intended as public nominal facts. Game-only values are labeled accordingly.

## Protection

Modern composite armor is not represented as claimed real millimeter/RHAe values. Each zone instead has synthetic 0–1000 kinetic and chemical protection indexes.

Zones can include hull front/side/rear/roof/floor, turret front/side/rear/roof and mantlet. Runtime component integrity is separate from the static protection definition.

## Damage

A vehicle can have dozens of component health records such as:

- crew stations;
- engine/transmission;
- left/right tracks or wheel groups;
- turret ring;
- gun breech/barrel;
- ammunition storage;
- fuel system;
- electrical system;
- fire-control computer;
- commander/gunner/driver optics;
- APS sensors/launchers;
- radio/datalink.

This supports mobility kills, sensor degradation, weapon disablement and partial damage without one global HP number.

## APS

APS state includes installed/powered status and a **synthetic game countermeasure budget**. Public qualitative coverage descriptions can be stored, but real countermeasure inventory or classified engagement envelopes are not inferred.

## Signatures

Visual, thermal, radar and acoustic signatures are normalized game values. The radar field is explicitly not a real RCS measurement. Future versions can make signatures directional and state-dependent (engine on/off, moving, damaged, smoke, vegetation, etc.).
