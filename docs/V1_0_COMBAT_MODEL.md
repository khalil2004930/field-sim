# v1.0 infantry engagement model

The v1.0 combat loop is intentionally game-engine level rather than an operational firing solution.

1. World/XYZ position and terrain context.
2. Geometric 3D LOS: Clear, Obscured or Blocked.
3. Sensor/detection pass using faction-specific knowledge.
4. Environment modifies visual/thermal acquisition.
5. A combat-capable soldier may engage a currently known hostile infantry contact.
6. Weapon fire uses cyclic rate for burst timing and practical range as an AI employment preference.
7. Hit probability combines synthetic precision/handling, shooter state, target concealment, LOS obscuration and range fraction.
8. Misses and fire volume create suppression.
9. Hits choose a body region and create a wound; armor is a normalized protective modifier rather than a real penetration table.
10. Wounds affect HP summary, bleeding, pain and shock.
11. Physiology evolves each simulated second and can cause unconsciousness, incapacitation or death.
12. Nearby medics/self-aid may stabilize bleeding at an abstract level.
13. Combat events are logged without exposing opponent-only faction knowledge.

## Optics and night

A firearm's projectile does not lose physical range because it is dark. The shooter instead becomes less capable of finding, identifying and precisely engaging the target. Therefore FieldSim applies darkness to sensing/acquisition, not to `MaximumPhysicalRangeMeters`.

- Bare visual optics depend strongly on ambient light.
- Night-vision optics mitigate darkness but still depend on ambient light and degrade in fog/dust.
- Thermal optics are largely independent of visible light but depend on thermal contrast and are degraded in rain/fog.

## Synthetic-data boundary

The default rifles, machine guns, marksman rifles, armor and sensor values are fictionalized baseline values used to exercise the engine. They are not presented as specifications for a current military force.
