# FieldSim v1.3 — ORBAT command prototype

v1.3 makes ORBAT a first-class interface rather than a reference-only tree.

## What changed

- The right-side command panel opens on a new **ORBAT** tab.
- Blue uses a NATO-style hierarchy down to platoon: side / brigade / battalion / company / platoon.
- Red uses an irregular scenario hierarchy: command groups / sectors / squads / teams / cells / support elements.
- Red prototype roles include mixed squads, anti-armor squads, recon squads, sniper/marksman teams, UAV cells, FPV cells, fires-support elements, signals, intelligence and medical/logistics support.
- Three view depths are available: Strategic, Tactical and Detail.
- ORBAT nodes use compact affiliation frames with role abbreviations and echelon labels. These are APP-6-inspired UI cues, not a claim of exact symbol-standard compliance.
- Selecting a node displays personnel, readiness, morale, ammunition, communications, current order and a live link to tactical entities assigned beneath that branch.
- The current tactical battle updates ORBAT live summary information: battle phase, effective Blue/Red entities and first-contact time.
- The prototype assigns Blue tactical entities to platoon nodes and Red tactical entities to squad/team nodes so losses and wounds can be viewed through the command hierarchy.

## Data boundary

`data/scenarios/orbat_nato_prototype.json` is a synthetic task-organization dataset for interface and software testing. It is not an asserted current real-world ORBAT, deployment, strength table, location picture or tactical procedure.

The public IDF peacetime reference tree remains separately available under the **IDF tree** tab.

## Next ORBAT work

- persistent formation IDs on scenario entities instead of temporary UI binding;
- command messages and acknowledgements moving through the ORBAT;
- intelligence reports propagating up and down the hierarchy;
- support-request queues for fires, UAV/FPV, medical and logistics elements;
- attachments/detachments and temporary task groups;
- AAR snapshots of the ORBAT at any simulation time;
- future 3D client consuming the same ORBAT state.
