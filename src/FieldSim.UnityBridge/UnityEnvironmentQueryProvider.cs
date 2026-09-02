using System.Net.Sockets;
using System.Text.Json;
using FieldSim.Core;

namespace FieldSim.UnityBridge;

public sealed class UnityEnvironmentQueryProvider : IExternalEnvironmentQueryProvider, IDisposable
{
    private readonly object _sync = new();
    private readonly string _host;
    private readonly int _port;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;

    public UnityEnvironmentQueryProvider(string host = "127.0.0.1", int port = 47831)
    {
        _host = host;
        _port = port;
    }

    public bool Ping()
    {
        var response = Send(new BridgeRequest { Type = "ping" });
        return response is { Handled: true };
    }

    public bool TryGetGroundAltitude(double xMeters, double yMeters, out double altitudeMeters)
    {
        var response = Send(new BridgeRequest
        {
            Type = "groundHeight",
            Point = new BridgePoint(xMeters, yMeters, 0)
        });
        if (response is { Handled: true } && double.IsFinite(response.NumberValue))
        {
            altitudeMeters = response.NumberValue;
            return true;
        }

        altitudeMeters = 0;
        return false;
    }

    public bool TryEvaluateLineOfSight(
        Position3D observer,
        Position3D target,
        out LineOfSightResult result)
    {
        var response = Send(new BridgeRequest
        {
            Type = "lineOfSight",
            From = BridgePoint.From(observer),
            To = BridgePoint.From(target)
        });

        if (response is not { Handled: true })
        {
            result = null!;
            return false;
        }

        var state = string.Equals(response.Status, "clear", StringComparison.OrdinalIgnoreCase)
            ? LineOfSightState.Clear
            : LineOfSightState.Blocked;
        Position3D? blockingPoint = response.HasPoint && response.Point is not null
            ? response.Point.ToPosition3D()
            : null;

        result = new LineOfSightResult(
            state,
            observer.HorizontalDistanceTo(target),
            double.PositiveInfinity,
            blockingPoint,
            0,
            string.IsNullOrWhiteSpace(response.TextValue)
                ? "Unity environment query."
                : response.TextValue);
        return true;
    }

    public bool TryIsPointInsideStructure(Position3D point, out bool insideStructure)
    {
        var response = Send(new BridgeRequest
        {
            Type = "pointInsideStructure",
            Point = BridgePoint.From(point)
        });
        if (response is { Handled: true })
        {
            insideStructure = response.BoolValue;
            return true;
        }

        insideStructure = false;
        return false;
    }

    public bool TryIsWalkable(Position3D point, TacticalUnitClass unitClass, out bool walkable)
    {
        var response = Send(new BridgeRequest
        {
            Type = "walkable",
            Point = BridgePoint.From(point),
            UnitClass = unitClass.ToString()
        });
        if (response is { Handled: true })
        {
            walkable = response.BoolValue;
            return true;
        }

        walkable = false;
        return false;
    }

    public bool TryIsMovementSegmentBlocked(
        Position3D from,
        Position3D to,
        TacticalUnitClass unitClass,
        out bool blocked)
    {
        var response = Send(new BridgeRequest
        {
            Type = "movementBlocked",
            From = BridgePoint.From(from),
            To = BridgePoint.From(to),
            UnitClass = unitClass.ToString()
        });
        if (response is { Handled: true })
        {
            blocked = response.BoolValue;
            return true;
        }

        blocked = false;
        return false;
    }

    public bool TryFindPath(
        Position3D from,
        Position3D to,
        TacticalUnitClass unitClass,
        out IReadOnlyList<Position3D> waypoints)
    {
        var response = Send(new BridgeRequest
        {
            Type = "findPath",
            From = BridgePoint.From(from),
            To = BridgePoint.From(to),
            UnitClass = unitClass.ToString()
        });
        if (response is { Handled: true, BoolValue: true })
        {
            waypoints = response.Waypoints?
                .Select(point => point.ToPosition3D())
                .ToArray() ?? Array.Empty<Position3D>();
            return true;
        }

        waypoints = Array.Empty<Position3D>();
        return false;
    }

    public void Attach(TacticalState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        state.World.ExternalQueries = this;
    }

    public void Dispose()
    {
        lock (_sync) CloseConnection();
        GC.SuppressFinalize(this);
    }

    private BridgeResponse? Send(BridgeRequest request)
    {
        lock (_sync)
        {
            try
            {
                EnsureConnected();
                request.Id = Guid.NewGuid().ToString("N");
                _writer!.WriteLine(JsonSerializer.Serialize(request, _jsonOptions));
                _writer.Flush();
                var line = _reader!.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) throw new IOException("Unity bridge closed the connection.");
                var response = JsonSerializer.Deserialize<BridgeResponse>(line, _jsonOptions);
                if (response is null || !string.Equals(response.Id, request.Id, StringComparison.Ordinal))
                    throw new IOException("Unity bridge returned an invalid response.");
                return response;
            }
            catch (IOException)
            {
                CloseConnection();
                return null;
            }
            catch (SocketException)
            {
                CloseConnection();
                return null;
            }
        }
    }

    private void EnsureConnected()
    {
        if (_client is { Connected: true } && _reader is not null && _writer is not null) return;

        CloseConnection();
        _client = new TcpClient
        {
            NoDelay = true,
            ReceiveTimeout = 5000,
            SendTimeout = 5000
        };
        _client.Connect(_host, _port);
        var stream = _client.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    private void CloseConnection()
    {
        _writer?.Dispose();
        _reader?.Dispose();
        _client?.Dispose();
        _writer = null;
        _reader = null;
        _client = null;
    }

    private sealed class BridgeRequest
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public BridgePoint? Point { get; set; }
        public BridgePoint? From { get; set; }
        public BridgePoint? To { get; set; }
        public string UnitClass { get; set; } = "";
    }

    private sealed class BridgeResponse
    {
        public string Id { get; set; } = "";
        public bool Handled { get; set; }
        public bool BoolValue { get; set; }
        public double NumberValue { get; set; }
        public string Status { get; set; } = "";
        public string TextValue { get; set; } = "";
        public bool HasPoint { get; set; }
        public BridgePoint? Point { get; set; }
        public BridgePoint[]? Waypoints { get; set; }
    }

    private sealed record BridgePoint(double XMeters, double YMeters, double ZMeters)
    {
        public static BridgePoint From(Position3D point) => new(point.X, point.Y, point.Z);
        public Position3D ToPosition3D() => new(XMeters, YMeters, ZMeters);
    }
}
