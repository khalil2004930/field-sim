# v1.10 historical design inputs — safe simulation translation

This note records how public historical research on the **2006 Battle of Bint Jbeil** influenced FieldSim mechanics. It does **not** encode a historical attack plan or reproduce exact tactical positions.

## Historical observations used at a high level

Public accounts consistently make several points useful to a simulation designer:

- physically entering an urban area did not automatically mean uncontested control;
- the battle involved intense close/urban fighting and difficult observation;
- command estimates and public claims about control could differ from the situation experienced by units on the ground;
- casualties and evacuation created significant friction;
- ground formations could become locally disorganized even while higher headquarters continued to issue objectives;
- reconnaissance/intelligence did not remove uncertainty;
- heavy supporting fires did not, by themselves, guarantee durable ground control;
- changing objectives, time pressure and incomplete information can make tactical success/failure difficult to measure in a single binary flag.

Public orientation/reference material includes the Wikipedia article on the 2006 battle and Matt M. Matthews, *We Were Caught Unprepared: The 2006 Hezbollah-Israeli War* (U.S. Army Combat Studies Institute). Social-media/archival commentary can be useful for leads, but unsupported claims are not treated as authoritative simulation facts.

## Translation into FieldSim v1.10

| Historical design lesson | Safe FieldSim mechanic |
| --- | --- |
| Presence is not the same as control | Multi-stage objective progress + contested/lost states |
| Urban observation is difficult | Synthetic building LOS blockers + concealment + contact confidence |
| Local knowledge differs from HQ picture | Entity-local contacts + delayed/lossy abstract reports |
| Units can become disrupted | Cohesion, morale, suppression and regroup behavior |
| Casualties consume time/attention | Treatment states + abstract evacuation request pipeline |
| Support does not equal control | Support requests/missions remain separate from objective ownership |
| Command intent can arrive late | Delayed high-level order delivery + AI intent journal |
| Battle narratives can be disputed/confusing | Detailed semantic AAR + full diagnostic export |

## Deliberate exclusions

FieldSim does not import exact historical/current defensive sites, attack routes, firing positions, real command frequencies/procedures, weapon release parameters, or current force locations. The public Bint Jbeil name is a geographic anchor; lower-level tactical geometry is fictional.
