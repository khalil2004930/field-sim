# Weapon evidence model

The weapon layer separates real-world evidence from scenario assumptions. `WeaponSystem` is the common base, with concrete classes for rockets, ballistic/guided missiles, cruise missiles, fixed-wing drones, FPV drones, mortars, anti-tank guided missiles, and guns.

Aliases and family identifiers prevent name collisions and accidental variant merging. For example, the Raad-2/3 artillery rockets and the Iranian Raad ATGM are separate records, while Sagger and Malyutka are aliases for the same base system.

## Why accuracy is not one field

| Measure | Meaning in this project |
|---|---|
| Circular error probable | Stored only when a source explicitly reports a CEP value and its scope is retained |
| Impact area | A reported width/length footprint; never converted into CEP |
| Operator-dependent | Directly or continuously guided systems where crew proficiency and visibility matter |
| Unknown | The selected public evidence does not support a value |
| Not applicable | The concept does not fit the system or modeled behavior |

## Realistic and manual modes

Realistic mode preserves public, historical, aggregate evidence and nullable unknowns. It does not estimate current stocks or operational readiness. Qualitative weather sensitivity may use category-level engineering evidence, but realistic mode will not generate a numeric weather-adjusted range without a sourced weapon-specific curve.

Manual mode can override range, CEP, reliability, operator burden, and environmental sensitivity. An optional weather multiplier enables numeric what-if runs, and the result is labeled as a manual assumption.

## Evidence discipline

Each system has one or more sources, an evidence status, confidence, caveats, and a boundary statement. Secondary reports, government claims by a belligerent, and social-media posts are not treated as equivalent. The initial catalog deliberately leaves all current quantities unknown.

## Research queue

`WeaponCandidateRecord` is deliberately not a `WeaponSystem`. A candidate cannot enter realistic-mode simulation until its designation and faction attribution are adequately sourced. Product-only candidates may retain a short advertised specification summary for comparison, but that does not imply possession.

This distinction is especially important for the attached 2013-2016 Iranian catalogue. It supports advertised Toophan and Saeghe variant differences, while separate evidence is still required to attribute an exact variant to Hezbollah.
