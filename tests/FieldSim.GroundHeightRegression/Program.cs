using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using FieldSim.Core;
using FieldSim.UnityBridge;

// Exercise the real TCP provider, including ID matching, JSON decoding and world fallback.
// Unity EditMode tests separately verify that terrain coverage produces these responses.
await Check("valid terrain", true, 135, 50, 60, 135);
await Check("zero-meter terrain", true, 0, 50, 60, 0);
await Check("unregistered terrain", false, 0, 50, 60, 321);
await Check("outside terrain bounds", false, 0, 150, 60, 321);
await Check("outside synthetic grid uses existing clamping", false, 0, -50, 250, 321);
Console.WriteLine("All 5 ground-height regression cases passed.");

static async Task Check(string name, bool handled, double numberValue, double x, double y, double expected)
{
    using var listener = new TcpListener(IPAddress.Loopback, 0);
    listener.Start();
    var port = ((IPEndPoint)listener.LocalEndpoint).Port;
    var server = Task.Run(async () =>
    {
        using var client = await listener.AcceptTcpClientAsync();
        using var stream = client.GetStream();
        using var reader = new StreamReader(stream);
        using var writer = new StreamWriter(stream) { AutoFlush = true };
        // One direct provider query followed by one query through TacticalWorldModel.
        for (var i = 0; i < 2; i++)
        {
            var line = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(10));
            using var request = JsonDocument.Parse(line ?? throw new Exception("Missing request."));
            var root = request.RootElement;
            Require(root.GetProperty("type").GetString() == "groundHeight", name + ": query type");
            var point = root.GetProperty("point");
            Require(point.GetProperty("xMeters").GetDouble() == x, name + ": X coordinate");
            Require(point.GetProperty("yMeters").GetDouble() == y, name + ": Y coordinate");
            Require(point.GetProperty("zMeters").GetDouble() == 0, name + ": initial elevation");
            await writer.WriteLineAsync(JsonSerializer.Serialize(new
            {
                id = root.GetProperty("id").GetString(),
                handled,
                numberValue,
                status = handled ? "ok" : "unavailable"
            }));
        }
    });

    using var provider = new UnityEnvironmentQueryProvider(port: port);
    Require(provider.TryGetGroundAltitude(x, y, out var altitude) == handled, name + ": availability");
    if (handled) Require(altitude == numberValue, name + ": sampled elevation");
    var world = new TacticalWorldModel(2, 2, 100);
    for (var gy = 0; gy < 2; gy++)
        for (var gx = 0; gx < 2; gx++)
        {
            var cell = new GridPoint(gx, gy);
            world.SetCell(cell, 321, world.Context(cell));
        }
    world.ExternalQueries = provider;
    Require(world.GroundAltitudeAt(x, y) == expected, name + ": world elevation");
    await server.WaitAsync(TimeSpan.FromSeconds(10));
    Console.WriteLine("PASS: " + name);
}

static void Require(bool condition, string message)
{
    if (!condition) throw new Exception("FAIL: " + message);
}
