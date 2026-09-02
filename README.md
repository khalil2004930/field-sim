# FieldSim v1.10 — Urban Combat, C2 & Diagnostics

FieldSim v1.10 builds on the v1.9.3 placement repair. The public Bint Jbeil map anchor remains geographic context; lower-level positions and all new urban geometry remain fictionalized/synthetic.

This release is focused on **gameplay friction**, not adding more catalog entries. It makes the ground fight less omniscient and less binary, gives the urban area physical LOS/movement consequences, adds cohesion/casualty/support state, and adds a one-click report that can be attached in chat when a run behaves badly.

## Main changes

- **Local knowledge before faction knowledge.** A sensor detection first belongs to the observing entity. A contact report then moves through an abstract command network with delay and possible loss before entering the shared faction picture.
- **Contact confidence.** Contacts can be Suspected, Probable, Confirmed, or Stale based on age and confidence.
- **Meter-native tactical AI.** Suppressed units look for cover within tens of meters, firing-position searches are meter-native, regrouping uses nearby friendly geometry, and movement can detour around synthetic urban structures without using the hidden 1 km planning cell as the tactical distance unit.
- **Synthetic urban district.** A deterministic fictional block pattern is generated around the public settlement anchor. Buildings block LOS and direct movement; each carries compartment/portal metadata as a foundation for later room-level CQB. The geometry is not copied from real building footprints.
- **Objective phases instead of one binary capture flag.** Objectives now track Unreached, Approaching, Entered, EstablishingPresence, Clearing, Held, Secured, Contested, and Lost. Empty objectives slowly lose control memory instead of staying permanently secured.
- **Cohesion and morale.** Nearby friendlies, leaders, suppression, fatigue, wounds, and nearby casualties affect local cohesion and morale. Very low cohesion/morale can trigger regrouping behavior.
- **Casualty/logistics state.** Wounded/incapacitated soldiers move through casualty states. Incapacitated casualties can create abstract evacuation requests. Ammo/water/medical equipment contribute to supply readiness.
- **Operational support request pipeline.** Requests move through Transmitting → Acknowledged → Assigned → Executing → Completed using same-side compatible synthetic assets. Timing is deliberately normalized and not a real procedure model.
- **Delayed formation orders.** Browser-issued formation orders remain high-level messages first; delivery and subordinate intent creation happen after the simulated command delay.
- **Visible urban/C2 status in the web UI.** The map can display synthetic buildings; the inspector/status panels show morale, cohesion, supply and casualty state; support shows the operational request pipeline.
- **Full REPORT / AAR button.** The top bar now has `REPORT / AAR`. Press it after something weird happens. It captures a unique run ID, scenario package ID and deterministic seed, the current snapshot, retained activity/combat history, semantic journal, local contacts, shared contacts, contact-report history, command-message history, support requests, AI intents, objectives, units, support state, performance counters, automatic integrity findings, and browser-side errors. You can copy it or download `FieldSim_v1_10_report_T<tick>.json` and attach that file in chat.

## Historical design input

Public historical material on the 2006 Bint Jbeil fighting was used as **design input for friction**, not as a script for recreating real attack routes or firing positions. The design lessons used here are broad: urban control is ambiguous, information can arrive late, units can become disorganized, casualty evacuation consumes attention/time, support does not automatically equal ground control, and entering a town is not the same as securing it.

See `docs/V1_10_HISTORICAL_DESIGN_INPUTS.md` for the safe translation from historical observations to fictional simulation mechanics.

## Data boundary

- Bint Jbeil is a public place anchor only.
- All lower-level combat placements, defensive/approach zones, support sectors, aircraft orbits, building footprints, rooms and portals are synthetic.
- C2 timing, message loss, support timing, casualty handling, cohesion, morale and logistics coefficients are normalized game abstractions.
- The release does not encode current real-world deployments, exact firing sites, frequencies, release parameters, target coordinates, or real tactical procedures.

## Run on Windows

```bat
build_windows.bat
launch_web.bat
```

Then open `http://localhost:5085`.

The artifact environment used to package this release does **not** contain the .NET SDK/compiler. `build_windows.bat` on a Windows machine with the .NET 10 SDK remains the authoritative compile + test check.

## How to report a bad run

1. Leave the sim at the moment the behavior looks wrong (running or paused is fine).
2. Press **REPORT / AAR** in the top bar.
3. Press **Download JSON**.
4. Attach that JSON in chat and briefly say what looked wrong, for example: “Blue got stuck outside the town around T+430.”

The report is designed so the exact run state can be inspected without guessing from a screenshot alone.
