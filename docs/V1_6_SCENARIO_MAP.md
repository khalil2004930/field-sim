# FieldSim v1.6 - Scenario ORBAT Map

This release replaces the custom canvas battlefield as the primary web view with a clean-room MapLibre scenario map.

## Architecture

- `FieldSim.Core` and the tactical engine remain authoritative.
- `FieldSim.Web` uses MapLibre GL JS 6.6.0 with an OpenStreetMap raster basemap.
- Local simulation X/Y is projected to the basemap for **display only**.
- Orders continue to use the local simulation grid. Geographic map clicks are not converted into targeting/LOS coordinates.
- ORBAT nodes and support elements are rendered as independent FieldSim-created military-style symbols.
- The left rail provides ORBAT, layers, and scenario status.
- The bottom dock provides a compact live simulation feed and scenario timeline.

## Scenario data boundary

The Bint Jbeil interface test uses public formation-level names as labels. Companies, platoons, squads, support relationships, strength values, and map placements are synthetic software-test data. No exact current battlefield dispositions are represented.

## External runtime dependencies

The browser needs internet access for:

- MapLibre GL JS: https://maplibre.org/maplibre-gl-js/docs/
- OpenStreetMap raster tiles: https://www.openstreetmap.org/copyright

The application displays OpenStreetMap attribution through MapLibre.
