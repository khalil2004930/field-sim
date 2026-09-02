# FieldSim v1.9.1 — Visible Joint Support

This release fixes the main v1.9 usability problem: the joint-support systems existed mostly as hidden runtime state, so the interface could look almost identical to v1.8.

## What is now visible immediately

- A persistent **THEATER SUPPORT · ABSTRACT ZONES** board appears over the map.
- The board shows the synthetic counter-battery radar, M109 Doher support platoon, two frontline Hermes 450 ISR aircraft, one armed border-response Hermes 450, and two F-16I CAP aircraft.
- The Support tab lists all five Hermes 450s and six F-16I scenario aircraft with role, fuel, stores, availability and status.
- A new Blue **Joint Support Group** branch appears in the ORBAT with radar, M109, Hermes and F-16I children.
- Clicking a support card selects the corresponding ORBAT branch; the inspector shows the live support assets under that node.
- Counter-battery cues are displayed as broad uncertainty values rather than exact launcher coordinates.
- A **Simulate rocket launch** test button runs the synthetic Red launch → radar cue → friendly-risk gate → response-asset selection chain instantly.

## Response priority

The synthetic support allocator protects the two frontline ISR aircraft from routine diversion:

1. Hermes 450-03 on the border-response orbit.
2. A ground-ready armed Hermes 450 if the response aircraft is unavailable.
3. An F-16I already on CAP only when no eligible armed UAV is available.

This is a game-level priority abstraction, not a real-world procedure.

## Fires

The M109 support element now creates real `JointSupportMission` runtime records, consumes abstract mission packages, becomes busy during the mission, and returns to ready state when complete. Missions are screened by the same generic friendly-position uncertainty system. Exact firing data and weapon solutions are intentionally not represented.

## Counter-battery

The radar is represented only in an abstract Blue rear / Israeli-side scenario zone. It creates deliberately imprecise local-simulation launch-area cues. No real sensor location, mode, accuracy figure or current deployment data is encoded.

A completed synthetic strike can reduce the scenario readiness/ammunition state of the Red rocket-support ORBAT node. This is deliberately coarse battle-state feedback rather than a real damage model.

## Validation boundary

The release environment has Node.js but no .NET SDK, csc or mcs. JavaScript syntax, JSON, ORBAT graph integrity and basic C# delimiter checks were run. A real C# compiler pass must still be performed with `build_windows.bat` on the user's .NET 10 machine.
