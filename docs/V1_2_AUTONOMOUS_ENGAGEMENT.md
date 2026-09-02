# v1.2 autonomous engagement

## Scenario flow

1. **Deployment** — Blue and Red receive distinct pre-battle positions around G07. Routes are already visible while paused.
2. **Advance** — both elements move from their respective controlled areas.
3. **Contact** — faction-specific sensors create the first contact; this does not automatically give the other faction reciprocal knowledge.
4. **Engagement** — combat-effective soldiers with a current contact and geometric LOS fire within practical employment range.
5. **Resolution** — casualties or elapsed time push the battle toward a decision.
6. **Complete** — objective capture, loss of combat effectiveness or the scenario time limit produces a result and stops movement.

## Orders and decisions

Each unit has a `UnitCommandState` containing its order, objective cell, current action, status text, contact and decision timing. The default mission assigns separate cells instead of sending everyone to one occupied destination.

Every simulated second the AI can:

- continue its ordered route;
- halt to engage a detected infantry contact;
- search within three cells for an unoccupied firing position with LOS and practical weapon range;
- move toward better cover when heavily suppressed;
- move a medic adjacent to a bleeding friendly;
- hold on reaching its assigned position;
- stop an incapacitated or dead unit.

The simulation remains deterministic: the same village seed, environment and orders produce the same movement and event stream.

## Objective and outcome

G07 is captured after 20 consecutive simulated seconds with an effective element inside the objective radius and no effective opponent contesting it. A side also loses when it has no combat-effective soldiers, or one remains against at least three opponents after the initial engagement. At 600 simulated seconds, remaining combat-effective soldiers and accumulated objective control decide the result.

## Visual language

- Blue marker: Blue element.
- Red marker: Red element.
- Dashed route: current order path.
- Gold dashed circle: objective control zone.
- Gold tracer: fire event during the last two ticks.
- Red ring: hit event during the last two ticks.
- Green/amber/red bar: soldier HP summary.
- Gray cross: dead soldier.
- Yellow outline: alive but not combat effective.

The village terrain, combat probabilities and equipment relationships remain synthetic simulation abstractions. The new behavior makes the existing model observable; it does not turn the local grid into a claim about real deployments or real terrain.
