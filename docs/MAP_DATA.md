# Theater map data

FieldSim v1.1 uses a small offline starter package under `data/maps/theater`. It renders immediately, while large optional public datasets stay outside source control and can be generated locally.

## Included layers

| Layer | Source | License | Role |
|---|---|---|---|
| Lebanon extract outline | Geofabrik / OpenStreetMap contributors | ODbL 1.0 | Theater reference boundary |
| Israel and Palestine extract outline | Geofabrik / OpenStreetMap contributors | ODbL 1.0 | Theater reference boundary |
| City and scenario-village points | Public geographic reference | Project data | Labels and tactical-grid handoff |

The `.poly` files describe Geofabrik download-extract extents. They are useful reference outlines, not legal or political boundary claims.

## Optional detailed layers

Run this in PowerShell from the project root after installing GDAL/OGR (the OSGeo4W shell supplied with QGIS is suitable):

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\prepare_theater_map.ps1
```

Add South Lebanon GLO-30 contours:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\prepare_theater_map.ps1 -IncludeElevation
```

The script downloads the current Geofabrik Lebanon and Israel/Palestine PBF extracts, clips and simplifies public road/water/settlement features, and writes GeoJSON into `data/maps/theater/generated`. With `-IncludeElevation`, it downloads four Copernicus DEM GLO-30 COG tiles covering the South Lebanon view and generates 50-meter contours.

Sources:

- OpenStreetMap attribution and ODbL: https://www.openstreetmap.org/copyright
- Geofabrik Lebanon: https://download.geofabrik.de/asia/lebanon.html
- Geofabrik Israel and Palestine: https://download.geofabrik.de/asia/israel-and-palestine.html
- Copernicus DEM GLO-30: https://registry.opendata.aws/copernicus-dem/
- ESA WorldCover 10 m (planned land-cover adapter): https://esa-worldcover.org/en/data-access

## Terrain truth model

Three different things must not be collapsed into one value:

1. **Elevation and slope** come from a DEM such as Copernicus GLO-30.
2. **Land cover** comes from a classified product such as ESA WorldCover and describes categories like built-up, trees, grassland or water.
3. **Ground/geologic material** requires a separate, properly sourced geology or soil dataset. It cannot be inferred reliably from elevation or religious/demographic area labels.

v1.1 only renders elevation contours. It does not yet feed them into LOS, mobility, penetration, blast or damage calculations. This keeps the simulator honest while the raster sampling and provenance model are built.

## Safety and scenario-data boundary

The map package accepts ordinary public geography. It must not become a database of current military deployments, real target lists, strategic-weapon facilities, inferred hidden sites, active routes, or guessed locations based on a community's religion or ethnicity. Fictional scenario facilities should use synthetic local coordinates or coarse fictional sectors and be clearly labeled as such.
