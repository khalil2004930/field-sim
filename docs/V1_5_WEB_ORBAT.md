# FieldSim v1.5 - Web ORBAT demonstrator

FieldSim v1.5 changes the primary interface direction from WPF to an original ASP.NET Core web command surface.

## Clean-room boundary

The web interface was authored for FieldSim from scratch. It does not include, import, patch, redistribute, or depend on the separately supplied third-party scenario-editor application files. Those files are not part of this package.

## What is live

- ASP.NET Core hosts the browser UI and the existing FieldSim C# simulation.
- The existing `TacticalEngine.Step` loop runs server-side.
- Browser state is streamed using Server-Sent Events at `/api/stream`.
- The ORBAT tree is loaded from FieldSim's own `data/scenarios/orbat_nato_prototype.json`.
- Simulated entities are linked to leaf ORBAT nodes; parents aggregate live strength/status.
- Selecting an ORBAT node highlights every attached descendant entity on the map.
- Selecting a map entity opens its runtime condition, action, ammunition, and current order.
- High-level Hold, Advance, Defend, Support, and Withdraw orders can be sent to linked ORBAT nodes.
- The event/AAR panel merges tactical activity and combat events.

## Map boundary

The bundled tactical view is a fictionalized, non-georeferenced local XYZ simulation map. The web project is intentionally structured so a licensed/public geospatial renderer can later be added as a separate map-provider adapter without making the geospatial client authoritative for simulation physics.

## Why SSE first

The first web prototype uses browser-native Server-Sent Events so it has no npm build step and no additional realtime JavaScript package. SignalR can replace or supplement the stream when multiplayer/user-specific channels are introduced.

## Run

`launch_web.bat`

or

`dotnet run --project src/FieldSim.Web/FieldSim.Web.csproj --urls http://localhost:5085`
