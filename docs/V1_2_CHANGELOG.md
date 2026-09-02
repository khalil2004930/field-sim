# FieldSim v1.2 changelog

## The problem corrected

In v1.1, `AiEnabled` only allowed a soldier to fire after an opponent was already detected and within practical range. No system issued movement orders or pursued an objective. On maps with blocked initial LOS, every unit could remain stationary indefinitely. The interface also labeled a 100-ms timer as 1×, making one simulated second pass ten times faster than real time.

## Engine changes

- Added `TacticalAiEngine` with deterministic pre-battle orders and replanning.
- Added command, action, phase, objective, activity-event and battle-result models.
- Added autonomous advance, hold, search-for-LOS, suppression/cover and casualty-support behavior.
- Added timed objective control, combat-ineffective victory, decision phase and 10-minute score limit.
- Added typed combat events for fire, hit, reload, medical, casualty and disabled states.
- Added first-contact and phase transition events.
- Repositioned opposing elements into coherent north/south starting areas.
- Added a flat, open synthetic crossroads engagement area so every village seed can produce a readable meeting engagement while the rest of its terrain remains deterministic.
- Preserved manual movement as an observer order override.

## Interface changes

- Both factions are visible by default; faction fog remains optional.
- Correct 0.5×/1×/2×/4× playback intervals, with 2× as the default.
- Readable combo-box and selected-tab text.
- Mission, phase, force-strength, objective-control and result HUD.
- Objective zone, preplanned paths, interpolated movement, role codes, action labels and health bars.
- Recent-fire tracers, hit flashes and persistent casualty markers.
- Live-action overlay and typed event-log entries.

## Validation additions

The executable regression suite now verifies that every unit receives an order, infantry receive routes, autonomous units move, the battle reaches advance/contact/fire/casualty states and identical seeds reproduce positions and event streams.
