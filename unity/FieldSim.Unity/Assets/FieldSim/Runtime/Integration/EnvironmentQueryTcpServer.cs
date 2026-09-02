using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using FieldSim.Unity.Core;
using FieldSim.Unity.Environment;
using UnityEngine;
using UnityEngine.AI;

namespace FieldSim.Unity.Integration
{
    /// <summary>
    /// Localhost-only bridge that lets the .NET simulation ask Unity for physical-world queries.
    /// Unity API calls are executed on the main thread; socket work stays on background threads.
    /// </summary>
    public sealed class EnvironmentQueryTcpServer : MonoBehaviour
    {
        [SerializeField] private int port = 47831;
        [SerializeField] private bool startOnAwake = true;
        [SerializeField] private FieldSimWorldSpace worldSpace;
        [SerializeField] private EnvironmentQueryService environmentQueries;

        private readonly ConcurrentQueue<PendingRequest> pending = new ConcurrentQueue<PendingRequest>();
        private TcpListener listener;
        private Thread listenerThread;
        private volatile bool stopping;

        private void Awake()
        {
            ResolveDependencies();
            if (startOnAwake) StartServer();
        }

        private void Update()
        {
            ResolveDependencies();
            int processed = 0;
            while (processed < 64 && pending.TryDequeue(out PendingRequest request))
            {
                request.ResponseJson = HandleRequest(request.RequestJson);
                request.Completed.Set();
                processed++;
            }
        }

        private void OnDestroy()
        {
            StopServer();
        }

        public void StartServer()
        {
            if (listenerThread != null) return;
            stopping = false;
            listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listenerThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "FieldSim Unity environment bridge"
            };
            listenerThread.Start();
            Debug.Log("FieldSim Unity environment bridge listening on 127.0.0.1:" + port);
        }

        public void StopServer()
        {
            stopping = true;
            try { listener?.Stop(); } catch (SocketException) { }
            listener = null;
            listenerThread = null;

            while (pending.TryDequeue(out PendingRequest request))
            {
                request.ResponseJson = ErrorResponse("", "Unity environment bridge stopped.");
                request.Completed.Set();
            }
        }

        private void AcceptLoop()
        {
            while (!stopping)
            {
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    Thread worker = new Thread(() => ServeClient(client))
                    {
                        IsBackground = true,
                        Name = "FieldSim Unity environment bridge client"
                    };
                    worker.Start();
                }
                catch (SocketException)
                {
                    if (!stopping) Thread.Sleep(100);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
            }
        }

        private void ServeClient(TcpClient client)
        {
            using (client)
            using (NetworkStream stream = client.GetStream())
            using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, false, 4096, true))
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false), 4096, true) { AutoFlush = true })
            {
                client.NoDelay = true;
                while (!stopping && client.Connected)
                {
                    string line;
                    try { line = reader.ReadLine(); }
                    catch (IOException) { return; }
                    if (line == null) return;

                    PendingRequest request = new PendingRequest(line);
                    pending.Enqueue(request);
                    if (!request.Completed.Wait(5000))
                    {
                        writer.WriteLine(ErrorResponse("", "Unity main thread did not answer the environment query in time."));
                        continue;
                    }

                    writer.WriteLine(request.ResponseJson ?? ErrorResponse("", "Unity environment query failed."));
                }
            }
        }

        private string HandleRequest(string json)
        {
            EnvironmentBridgeRequest request;
            try
            {
                request = JsonUtility.FromJson<EnvironmentBridgeRequest>(json);
            }
            catch (Exception exception)
            {
                return ErrorResponse("", "Invalid environment query JSON: " + exception.Message);
            }

            if (request == null || string.IsNullOrEmpty(request.type))
                return ErrorResponse(request != null ? request.id : "", "Environment query type is missing.");

            EnvironmentBridgeResponse response = new EnvironmentBridgeResponse
            {
                id = request.id,
                handled = true,
                status = "ok",
                textValue = "Unity 6 environment query.",
                waypoints = Array.Empty<FieldSimPosition>()
            };

            if (request.type == "ping")
            {
                response.textValue = "fieldsim-unity6";
                return JsonUtility.ToJson(response);
            }

            if (worldSpace == null || environmentQueries == null)
                return ErrorResponse(request.id, "Unity environment services are not ready.");

            switch (request.type)
            {
                case "groundHeight":
                {
                    Vector3 unityPoint = worldSpace.ToUnity(request.point);
                    float groundY = environmentQueries.SampleGroundHeight(unityPoint);
                    FieldSimPosition fieldPoint = worldSpace.ToFieldSim(new Vector3(unityPoint.x, groundY, unityPoint.z));
                    response.numberValue = fieldPoint.zMeters;
                    break;
                }
                case "lineOfSight":
                {
                    Vector3 from = worldSpace.ToUnity(request.from);
                    Vector3 to = worldSpace.ToUnity(request.to);
                    VisibilityResult result = environmentQueries.TraceVisibility(from, to);
                    response.status = result.IsClear ? "clear" : VisibilityStatusText(result.Status);
                    response.boolValue = result.IsClear;
                    response.textValue = "Unity Physics LOS: " + result.Status;
                    if (!result.IsClear)
                    {
                        response.hasPoint = true;
                        response.point = worldSpace.ToFieldSim(result.BlockPoint);
                    }
                    break;
                }
                case "pointInsideStructure":
                {
                    response.boolValue = environmentQueries.IsPointInsideStructure(worldSpace.ToUnity(request.point));
                    break;
                }
                case "movementBlocked":
                {
                    response.boolValue = environmentQueries.IsMovementSegmentBlocked(
                        worldSpace.ToUnity(request.from),
                        worldSpace.ToUnity(request.to));
                    break;
                }
                case "walkable":
                {
                    Vector3 point = worldSpace.ToUnity(request.point);
                    response.boolValue = NavMesh.SamplePosition(point, out NavMeshHit hit, 2.0f, NavMesh.AllAreas);
                    if (response.boolValue)
                        response.point = worldSpace.ToFieldSim(hit.position);
                    break;
                }
                case "findPath":
                {
                    response.boolValue = TryFindPath(request.from, request.to, out FieldSimPosition[] corners);
                    response.waypoints = corners;
                    break;
                }
                default:
                    response.handled = false;
                    response.status = "unsupported";
                    response.textValue = "Unsupported environment query type: " + request.type;
                    break;
            }

            return JsonUtility.ToJson(response);
        }

        private bool TryFindPath(FieldSimPosition from, FieldSimPosition to, out FieldSimPosition[] corners)
        {
            corners = Array.Empty<FieldSimPosition>();
            Vector3 fromUnity = worldSpace.ToUnity(from);
            Vector3 toUnity = worldSpace.ToUnity(to);
            if (!NavMesh.SamplePosition(fromUnity, out NavMeshHit startHit, 3.0f, NavMesh.AllAreas)) return false;
            if (!NavMesh.SamplePosition(toUnity, out NavMeshHit endHit, 3.0f, NavMesh.AllAreas)) return false;

            NavMeshPath path = new NavMeshPath();
            if (!NavMesh.CalculatePath(startHit.position, endHit.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete)
                return false;

            Vector3[] unityCorners = path.corners;
            corners = new FieldSimPosition[unityCorners.Length];
            for (int i = 0; i < unityCorners.Length; i++)
                corners[i] = worldSpace.ToFieldSim(unityCorners[i]);
            return true;
        }

        private void ResolveDependencies()
        {
            if (worldSpace == null) worldSpace = FindFirstObjectByType<FieldSimWorldSpace>();
            if (environmentQueries == null) environmentQueries = FindFirstObjectByType<EnvironmentQueryService>();
        }

        private static string VisibilityStatusText(VisibilityStatus status)
        {
            switch (status)
            {
                case VisibilityStatus.BlockedByTerrain: return "blockedByTerrain";
                case VisibilityStatus.BlockedByStructure: return "blockedByStructure";
                case VisibilityStatus.BlockedByOther: return "blockedByOther";
                default: return "clear";
            }
        }

        private static string ErrorResponse(string id, string message)
        {
            return JsonUtility.ToJson(new EnvironmentBridgeResponse
            {
                id = id,
                handled = false,
                status = "error",
                textValue = message,
                waypoints = Array.Empty<FieldSimPosition>()
            });
        }

        private sealed class PendingRequest
        {
            public PendingRequest(string requestJson)
            {
                RequestJson = requestJson;
            }

            public string RequestJson { get; }
            public ManualResetEventSlim Completed { get; } = new ManualResetEventSlim(false);
            public string ResponseJson { get; set; }
        }
    }
}
