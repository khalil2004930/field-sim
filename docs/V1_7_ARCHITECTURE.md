# FieldSim v1.7 architecture baseline

## Runtime layers

```text
ScenarioPackage / data provenance
            |
            v
     SimulationWorld (Core)
  continuous entity state + terrain
      |       |       |
      |       |       +-- semantic event journal
      |       +---------- sensor / LOS / detection / combat
      +------------------ spatial hash / navigation / AI
            |
            v
       ORBAT aggregation
 stable assignments + derived positions
            |
            v
       ASP.NET Core Web
 snapshot cache + orders + performance
            |
            v
          browser
 MapLibre + ORBAT + timeline/feed
```

## Stable identity

`TacticalUnit.Id` remains a runtime integer optimized for internal collections. `TacticalUnit.EntityKey` is the stable scenario identity used for authored assignments and ORBAT binding.

`ScenarioPackage` owns the binding between stable entity key and ORBAT node. This removes the v1.6 web server's hardcoded runtime-ID dictionary.

## Scenario package boundary

The v1.7 integrated scenario explicitly states that lower-level composition, local XYZ placement, and equipment allocation are synthetic scenario content. The public geographic basemap is display-only; local XYZ remains authoritative for LOS, movement and combat.

## Small-arms provenance -> runtime

The Hezbollah small-arms OSINT JSON is provenance data. `SmallArmsRuntimeAdapter` converts selected records into normalized simulation weapon definitions. This prevents public source identity/evidence from being confused with synthetic game performance coefficients.

Important separation:

```text
Weapon model
!= cartridge
!= projectile
!= magazine
!= optic
!= under-barrel weapon
```

The current runtime adapter is intentionally broad. Future ballistic/body-armor work should add separate cartridge/projectile definitions rather than stuffing terminal effects into the weapon identity record.

## AI direction

v1.7 introduces `AiIntent` and `AiIntentStore`. A high-level ORBAT order is represented as intent and translated to exact destinations for member entities. The intended hierarchy is:

```text
faction intent
 -> formation intent
   -> sub-formation task
     -> entity action
```

Upper layers should decide *what state is desired*. Lower layers decide local movement/action. Formations do not acquire independent physical positions.

## Replay/AAR direction

`SimulationJournal` is a bounded semantic event sequence with monotonically increasing sequence IDs. v1.7 still serves the existing activity/combat event views, but this journal is the future source for delta streaming, replay bookmarks, scenario scrubbing and AAR.

## Web scaling direction

v1.7 reduces browser work by:

- caching identical server snapshots by simulation revision;
- using MapLibre GeoJSON layers for individual dots and trails rather than one DOM marker per individual;
- applying zoom/echelon LOD to ORBAT symbols;
- deriving formation coordinates server-side;
- exposing per-tick simulation counters.

The next step is delta/event streaming so clients do not require a complete snapshot after every change.


## Web read-model optimization

The ASP.NET Core session maintains a revisioned snapshot cache. Formation membership and spatial state are batch-derived once per snapshot, and the SSE endpoint emits only when the revision changes (with periodic keepalive comments). The browser can therefore remain responsive while the simulation is paused without continuously receiving identical state. Future versions can replace full changed snapshots with entity/event deltas without changing the authoritative simulation model.
