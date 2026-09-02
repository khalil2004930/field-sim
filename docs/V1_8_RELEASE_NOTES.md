# FieldSim v1.8 — Open-Ended Tactical Core

## Outcome

v1.8 removes the match-style stopping rules from the web scenario and makes the tactical state suitable for a later campaign layer. Local control, casualties and combat events continue to update, but tactical objectives and force collapse do not declare a final winner.

## Active simulation changes

- The web scenario sets `TacticalState.OpenEndedScenario = true`.
- Five synthetic town-sector objectives replace the single terminal crossroads objective.
- IDF-side authored placements are on the local southern approach; Hezbollah-side placements are north/townward. Local Y increases northward.
- Formation commands are queued, delayed according to hierarchy depth and the authored communications label, acknowledged, and then applied to linked live entities.
- Superseded, failed, queued and delivered command messages are exposed in the web snapshot.
- Current entity routes and objective state are exposed to the map.
- Damaged road usability participates in path selection and continuous movement speed.

## Interface correction

v1.7 placed a public geographic basemap beneath synthetic terrain without showing the terrain that actually controlled the simulation. v1.8 renders the authoritative 25 m terrain cells over the basemap and labels the map policy clearly.

Only ORBAT nodes with directly attached runtime entities receive operational markers. Reference-only UAV, fires and support branches remain in the tree/catalog but are no longer drawn as if they were live assets.

The web client adds:

- `+1s` and `+10s` deterministic stepping;
- map-click order destinations;
- route, terrain, objective and basemap toggles;
- continuous town-control status;
- command-channel history;
- a support capability catalog that distinguishes modeled classes from instantiated assets.

## Ground force and infrastructure additions

The synthetic tactical scenario adds a two-person Hezbollah-side ATGM team under the anti-armor ORBAT branch. The operator has a separate `weapon:kornet_e` capability reference. This does not pretend that the rifle solver is an ATGM solver: projectile flight, guidance, armor interaction and effects remain pending.

`BuildingState` and `RoadSegmentState` add integrity, damage categories and repair. Buildings can author basements, bunkers and tunnel entrances. Roads distinguish foot and vehicle usability, include obstruction and capacity, and can represent bridge decks with stricter failure thresholds.

## Aviation, UAS, fires and evacuation foundation

Typed support assets and requests now cover:

- tube and rocket artillery;
- attack and rescue helicopters;
- tactical and MALE UAS;
- fixed-wing strike;
- casualty evacuation;
- medical and engineer repair teams;
- route and bridge repair.

The public-reference platform data adds Hermes 450, an AH-64 Israeli-operator reference and an IAF rescue-helicopter capability record. Hermes 900 was already present. The support catalog intentionally leaves scenario quantities, readiness, bases, operating zones and response times null unless a manual scenario authors them.

“Mikholit/Micholit” is retained only in an unresolved research queue. No range, accuracy, warhead, carrier or availability data is guessed.

## Still not active

- multi-town operational movement and persistent neighboring-town garrisons;
- artillery trajectories/effects and ammunition logistics;
- ATGM guidance, penetration and vehicle damage interaction;
- airspace, basing, sortie generation, flight paths and air-defense interaction;
- executable helicopter pickup-zone selection and casualty transport;
- building collapse geometry, debris generation and tunnel navigation;
- bridges imported from real geodata;
- political/economic campaign termination.

These boundaries are intentional. v1.8 provides typed state and honest interface distinctions so later solvers can be added without presenting static labels as completed capabilities.

## Public-reference sources added

- Elbit Systems, Hermes 450: https://www.elbitsystems.com/autonomous/aerial/tactical-uas/hermes-450
- Elbit Systems, Hermes 900: https://www.elbitsystems.com/autonomous/aerial/male-unmanned-aircraft-systems/hermes-900
- Boeing, AH-64 Apache: https://www.boeing.com/defense/military-rotorcraft/ah-64-apache
- Israel Defense Forces, rescue helicopters and Unit 669: https://www.idf.il/en/articles/operational-footage-of-rescue-efforts-by-iaf-helicopters-and-unit-669/

No source is used to infer current quantities, locations, readiness, sortie rates or assignments.
