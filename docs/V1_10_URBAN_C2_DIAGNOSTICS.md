# FieldSim v1.10 — Urban Combat, C2 & Diagnostics

## Goal

v1.10 turns the v1.9.3 geographically coherent scenario into a more useful gameplay/debugging sandbox. The primary goal is to model *friction* before adding more weapon/platform breadth.

## Implemented systems

### 1. Entity-local contacts + delayed shared picture

Sensors update the observing entity first. The C2 layer queues a synthetic contact report with normalized delay/loss. Delivered reports update the faction picture. Contact age/confidence produces Suspected / Probable / Confirmed / Stale states.

### 2. Synthetic urban physical layer

The scenario creates a deterministic fictional district around the public-place anchor. Structures:

- block direct LOS;
- block direct exterior movement;
- contribute local cover scores;
- contain floors/room compartments/portal metadata for future CQB.

The current movement solver routes **around** buildings. It does not yet enter rooms; room-level navigation is explicitly future work.

### 3. Meter-native local AI

Cover search, firing-position search and regrouping use tens-of-meters radii and the spatial index/continuous XYZ. Country-scale 1 km cells remain a legacy planning/terrain compatibility aid only.

### 4. Objective progression

Each side tracks:

`Unreached → Approaching → Entered → EstablishingPresence → Clearing → Held → Secured`

`Contested` and `Lost` are explicit states. Control counters decay when the area is abandoned or held by the opponent. Objective state transitions are recorded in the semantic journal/AAR.

### 5. Cohesion + morale

Cohesion reacts to local friendly density, leadership, suppression, fatigue, wounds and casualties. Morale moves toward a cohesion/discipline-derived target. Severely disrupted entities can regroup rather than blindly continue an advance.

### 6. Casualties + logistics

Soldiers expose casualty disposition, supply readiness and evacuation state. Incapacitated casualties can generate abstract evacuation requests. The system is intentionally normalized for simulation pacing and is not a reproduction of real medical procedures or timelines.

### 7. Operational support request pipeline

The generic support pipeline supports transmitting, acknowledging, assigning, executing and completing requests. Same-side compatibility is enforced. Existing visual joint-support aircraft/fires remain a separate runtime layer for now.

### 8. REPORT / AAR

The top-bar button calls `/api/diagnostics/report` and adds browser state. The downloadable JSON contains:

- version/build/report format, unique run ID, scenario package ID and deterministic seed;
- tick, phase, result, run speed;
- current full snapshot;
- retained semantic journal;
- retained activity events;
- retained combat events;
- local contact states;
- contact-report transmission history;
- shared faction contacts;
- command-message history;
- AI intents;
- operational support requests;
- objective states;
- unit positions/speeds/acceleration/actions/orders/morale/cohesion/supply/casualty status;
- joint-support assets/missions/cues/impacts;
- performance counters;
- automatic integrity findings;
- client-side JS errors and recent HTTP request results.

## Known limitations

- Full room/portal navigation and interior fighting are not active yet.
- Formation hierarchy is still shallow at the physical entity level; the scenario still contains a small integration force rather than multiple full squads/platoons.
- `OperationalSupportState` and `JointSupportState` are still parallel support abstractions.
- Vehicle combat remains incomplete.
- Terrain/elevation under the public basemap remains synthetic, so the map is context rather than authoritative tactical geometry.
- No .NET compiler was available in the packaging environment; Windows build/test is authoritative.
