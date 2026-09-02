# FieldSim v1.1 changelog

## Added

- WGS84 theater-map domain types, Web Mercator screen projection and geodesic distance.
- Manifest-driven offline map package loader for GeoJSON and Geofabrik `.poly` files.
- Included Lebanon and Israel/Palestine extract boundaries plus public settlement labels.
- Desktop **Theater geography** view with zoom, coordinate inspection, scale bar and South Lebanon shortcut.
- Double-click handoff from a public village label to its existing 13×13 local scenario.
- Optional OSM road/water/settlement and Copernicus elevation-contour preparation workflow.
- `map status [manifest-file]` CLI command.
- Projection, distance, package-loading, optional-layer and data-boundary regression checks.

## Preserved

All v1.0 engagement, wounds, suppression, equipment, detection, LOS, formation and vehicle behavior remains in place. The real theater layer does not silently replace the local tactical physics.

## Explicit boundaries

- The theater map is public geography, not a live operational picture.
- No current deployments, target lists, strategic-weapon facilities, hidden-site inference, patrol routes or faction-linked civilian locations are stored.
- Public settlement points are navigation labels only.
- Copernicus contours are visual until a later, tested terrain sampler binds elevation to tactical cells.
