# Clean-room interface and source boundary

FieldSim is independently authored.

No separately supplied third-party scenario-editor application source, repository code, JavaScript bundles, CSS bundles, assets, authentication logic, sharing logic, or private implementation details are included, imported, modified, or required at runtime.

FieldSim implements common product concepts in its own code: an ORBAT tree, a dominant web map, military-style scenario symbols, a timeline, a compact activity feed, a unit inspector, and a .NET simulation backend. Those are generic interface/architecture concepts rather than copied implementation.

## v1.7 rule

The v1.7 implementation continues the clean-room boundary. New continuous-world simulation, ORBAT aggregation, small-arms data integration, MapLibre rendering, performance instrumentation, scenario packaging, and future CQB/LOD contracts are FieldSim code and data structures.

When external open-source libraries or public data are used, their licenses and provenance must be reviewed separately before distribution.
