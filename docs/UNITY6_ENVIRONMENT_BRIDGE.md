# Unity 6 environment bridge

FieldSim keeps its simulation state in the existing .NET core while Unity 6 becomes authoritative for physical environment queries.

## Why a process boundary

`FieldSim.Core` currently targets .NET 10 and uses language/runtime features that should not be forced directly into Unity's scripting runtime. The migration therefore uses a small localhost bridge instead of copying the simulation code into Unity.

This keeps the simulation deterministic and headless-testable while allowing Unity to own terrain, colliders, NavMesh, buildings, bunkers, tunnels, portals, rubble and other physical geometry.

## Query flow

1. The simulation creates `UnityEnvironmentQueryProvider` from `FieldSim.UnityBridge`.
2. The provider is attached to `TacticalState.World.ExternalQueries`.
3. Existing world queries first ask Unity.
4. If Unity is unavailable or does not handle a query, FieldSim falls back to the v1.10 synthetic environment logic.

The current bridge supports:

- ground elevation;
- 3D line of sight through Unity Physics;
- point-inside-structure checks;
- NavMesh walkability;
- structure movement blocking;
- NavMesh path requests.

## Unity side

`EnvironmentQueryTcpServer` binds only to `127.0.0.1:47831`. Socket work occurs on background threads, while every Unity Physics/NavMesh call is queued back to the Unity main thread.

The server is automatically created in Editor and standalone players. It is not enabled for Web builds.

## .NET side

Example integration:

```csharp
using FieldSim.UnityBridge;

using var unityEnvironment = new UnityEnvironmentQueryProvider();
unityEnvironment.Attach(state);

if (!unityEnvironment.Ping())
    Console.WriteLine("Unity environment is unavailable; FieldSim will use its built-in fallback world.");
```

## Migration boundary

Unity answers geometry/navigation questions. It does not decide AI intent, C2/ISR state, morale, support requests, combat outcomes or scenario time.

The next migration step is to make `TacticalEngine.IssueMove*` prefer Unity NavMesh paths and to synchronize Unity entity GameObjects from immutable simulation snapshots. Combat remains disconnected from structural damage until the environment milestone is stable.
