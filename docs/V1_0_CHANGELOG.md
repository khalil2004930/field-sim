# FieldSim v1.0 changelog

v1.0 turns the v0.9 spatial/detection prototype into the first playable engagement simulation layer.

## Added

- `EnvironmentState` with light and visibility presets.
- Environment-aware visual, night-vision and thermal detection modifiers.
- `InfantryWeaponDefinition`, `WeaponRuntime`, `OpticProfile`, weapon classes and fire modes.
- Soldier roles, equipment, skills, carried mass and body armor.
- Body-region wounds, HP summary, blood loss, pain, shock, consciousness and incapacitation.
- Suppression and fatigue state.
- Abstract medical stabilization.
- `EngagementEngine` connected to the normal tactical tick.
- Deterministic combat event log.
- Day/night/weather controls in the WPF observer panel.
- Role-based fictional Blue/Red infantry test elements.
- `engagement` command in `FieldSim.Runner`.

## Important modeling choices

- LOS, detection, classification, identification and faction knowledge remain separate.
- A unit never learns that it has been detected merely because an opponent detected it.
- Night does not reduce projectile physical range. It changes acquisition/detection/identification.
- Practical engagement range guides AI target selection but is not modeled as a magical projectile cutoff.
- HP is a summary UI value; wounds, blood, shock and consciousness drive soldier condition.
- Public/real equipment catalogs remain separate from the synthetic combat model.
- Current vehicle entities share the same spatial/sensor world but vehicle weapon/damage combat is intentionally deferred.
