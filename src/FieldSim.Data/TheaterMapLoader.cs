using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using FieldSim.Domain;

namespace FieldSim.Data;

public static class TheaterMapLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static TheaterMapPackage LoadDefault(string dataRoot) =>
        Load(Path.Combine(dataRoot, "maps", "theater", "manifest.json"));

    public static TheaterMapPackage Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
            throw new FileNotFoundException("Theater-map manifest not found.", manifestPath);

        var manifest = JsonSerializer.Deserialize<MapManifest>(File.ReadAllText(manifestPath), JsonOptions)
            ?? throw new InvalidDataException("The theater-map manifest contained no data.");
        ValidateManifest(manifest);

        var root = Path.GetDirectoryName(Path.GetFullPath(manifestPath))!;
        var layers = new Dictionary<string, IReadOnlyList<MapFeature>>(StringComparer.OrdinalIgnoreCase);
        var missing = new List<string>();
        foreach (var descriptor in manifest.Layers)
        {
            var path = Path.GetFullPath(Path.Combine(root, descriptor.File));
            var relativePath = Path.GetRelativePath(root, path);
            if (relativePath == ".." || relativePath.StartsWith(".." + Path.DirectorySeparatorChar,
                    StringComparison.Ordinal))
                throw new InvalidDataException($"Map layer '{descriptor.Id}' escapes the package directory.");
            if (!File.Exists(path))
            {
                if (descriptor.Required)
                    throw new FileNotFoundException($"Required map layer '{descriptor.Id}' not found.", path);
                missing.Add(descriptor.Id);
                continue;
            }

            layers.Add(descriptor.Id, descriptor.Format.ToLowerInvariant() switch
            {
                "geojson" => ReadGeoJson(path),
                "geofabrik-poly" => ReadPoly(path, descriptor.DisplayName),
                _ => throw new InvalidDataException($"Unsupported map format '{descriptor.Format}'.")
            });
        }

        return new TheaterMapPackage
        {
            Manifest = manifest,
            Layers = layers,
            MissingOptionalLayers = missing
        };
    }

    private static void ValidateManifest(MapManifest manifest)
    {
        if (!manifest.Bounds.IsValid) throw new InvalidDataException("Theater-map bounds are invalid.");
        if (string.IsNullOrWhiteSpace(manifest.Name) || string.IsNullOrWhiteSpace(manifest.Version) ||
            string.IsNullOrWhiteSpace(manifest.AsOf) || string.IsNullOrWhiteSpace(manifest.DataBoundary))
            throw new InvalidDataException("Theater-map identity and data-boundary fields are required.");
        if (!string.Equals(manifest.CoordinateReferenceSystem, "EPSG:4326", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("FieldSim v1.1 map packages must use EPSG:4326 coordinates.");
        if (manifest.Layers.Select(layer => layer.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() !=
            manifest.Layers.Count)
            throw new InvalidDataException("Theater-map layer IDs must be unique.");
        var sourceIds = manifest.Attributions.Select(source => source.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var layer in manifest.Layers)
        {
            if (!sourceIds.Contains(layer.AttributionId))
                throw new InvalidDataException($"Layer '{layer.Id}' references an unknown attribution.");
        }
    }

    private static IReadOnlyList<MapFeature> ReadPoly(string path, string name)
    {
        var parts = new List<IReadOnlyList<GeoCoordinate>>();
        List<GeoCoordinate>? active = null;
        foreach (var rawLine in File.ReadLines(path))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.Equals("none", StringComparison.OrdinalIgnoreCase)) continue;
            if (line.Equals("END", StringComparison.OrdinalIgnoreCase))
            {
                if (active is { Count: >= 3 }) parts.Add(active);
                active = null;
                continue;
            }
            var values = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length == 1)
            {
                active = new List<GeoCoordinate>();
                continue;
            }
            if (active is null || values.Length < 2) continue;
            if (!double.TryParse(values[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude) ||
                !double.TryParse(values[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude))
                throw new InvalidDataException($"Invalid coordinate in '{path}'.");
            active.Add(new GeoCoordinate(latitude, longitude));
        }

        return
        [
            new MapFeature
            {
                GeometryType = MapGeometryType.Polygon,
                Parts = parts,
                Properties = new Dictionary<string, string> { ["name"] = name }
            }
        ];
    }

    private static IReadOnlyList<MapFeature> ReadGeoJson(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!root.TryGetProperty("type", out var type) || type.GetString() != "FeatureCollection")
            throw new InvalidDataException($"'{path}' is not a GeoJSON FeatureCollection.");

        var features = new List<MapFeature>();
        foreach (var featureElement in root.GetProperty("features").EnumerateArray())
        {
            if (!featureElement.TryGetProperty("geometry", out var geometry) || geometry.ValueKind == JsonValueKind.Null)
                continue;
            var geometryType = geometry.GetProperty("type").GetString() ?? "";
            var coordinates = geometry.GetProperty("coordinates");
            var properties = ReadProperties(featureElement);
            switch (geometryType)
            {
                case "Point":
                    features.Add(Create(MapGeometryType.Point, [ReadPoint(coordinates)], properties));
                    break;
                case "LineString":
                    features.Add(Create(MapGeometryType.LineString, [ReadLine(coordinates)], properties));
                    break;
                case "MultiLineString":
                    features.Add(Create(MapGeometryType.LineString,
                        coordinates.EnumerateArray().Select(ReadLine).ToArray(), properties));
                    break;
                case "Polygon":
                    features.Add(Create(MapGeometryType.Polygon,
                        coordinates.EnumerateArray().Select(ReadLine).ToArray(), properties));
                    break;
                case "MultiPolygon":
                    foreach (var polygon in coordinates.EnumerateArray())
                        features.Add(Create(MapGeometryType.Polygon,
                            polygon.EnumerateArray().Select(ReadLine).ToArray(), properties));
                    break;
            }
        }
        return features;
    }

    private static MapFeature Create(MapGeometryType type,
        IReadOnlyList<IReadOnlyList<GeoCoordinate>> parts, IReadOnlyDictionary<string, string> properties) =>
        new() { GeometryType = type, Parts = parts, Properties = properties };

    private static IReadOnlyDictionary<string, string> ReadProperties(JsonElement feature)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!feature.TryGetProperty("properties", out var properties) ||
            properties.ValueKind != JsonValueKind.Object) return values;
        foreach (var property in properties.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                values[property.Name] = property.Value.ToString();
        }
        return values;
    }

    private static IReadOnlyList<GeoCoordinate> ReadPoint(JsonElement element) => [ReadCoordinate(element)];
    private static IReadOnlyList<GeoCoordinate> ReadLine(JsonElement element) =>
        element.EnumerateArray().Select(ReadCoordinate).ToArray();

    private static GeoCoordinate ReadCoordinate(JsonElement element)
    {
        var values = element.EnumerateArray().ToArray();
        if (values.Length < 2) throw new InvalidDataException("GeoJSON coordinate has fewer than two ordinates.");
        var coordinate = new GeoCoordinate(values[1].GetDouble(), values[0].GetDouble());
        if (!coordinate.IsValid) throw new InvalidDataException("GeoJSON coordinate is outside WGS84 bounds.");
        return coordinate;
    }
}
