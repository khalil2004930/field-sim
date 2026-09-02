using System.Text.Json;
using System.Text.Json.Serialization;
using FieldSim.Domain;

namespace FieldSim.Data;

public static class ForceDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static ForceDataset LoadBaseline(string dataRoot)
    {
        var baseline = Path.Combine(dataRoot, "osint_baseline");
        var sources = Read<List<SourceRecord>>(Path.Combine(baseline, "sources.json"));
        var platforms = Read<List<PlatformRecord>>(Path.Combine(baseline, "platforms.json"));
        var dataset = new ForceDataset { Sources = sources, Platforms = platforms };
        ThrowIfInvalid(dataset);
        return dataset;
    }

    public static void ApplyOverrides(ForceDataset dataset, string overridePath)
    {
        var overrides = Read<List<ManualOverride>>(overridePath);
        foreach (var item in overrides)
        {
            ApplyOverride(dataset, item);
        }
        ThrowIfInvalid(dataset);
    }

    public static void ApplyOverride(ForceDataset dataset, ManualOverride item)
    {
        var platform = dataset.FindPlatform(item.PlatformId)
            ?? throw new InvalidDataException($"Unknown platform '{item.PlatformId}'.");

        switch (item.Field)
        {
            case "quantityMinimum": platform.Quantity.Minimum = item.Value; break;
            case "quantityLikely": platform.Quantity.Likely = item.Value; break;
            case "quantityMaximum": platform.Quantity.Maximum = item.Value; break;
            case "readinessPercent": platform.ReadinessPercent = item.Value; break;
            case "sustainmentIndex": platform.SustainmentIndex = item.Value; break;
            case "protectionIndex": platform.ProtectionIndex = item.Value; break;
            case "mobilityIndex": platform.MobilityIndex = item.Value; break;
            case "apsCoveragePercent": platform.ApsCoveragePercent = item.Value; break;
            case "enduranceHours": platform.EnduranceHours = item.Value; break;
            case "ceilingFeet": platform.CeilingFeet = item.Value; break;
            default: throw new InvalidDataException($"Unsupported override field '{item.Field}'.");
        }

        platform.OverrideCount++;
        dataset.TotalOverrides++;
    }

    private static T Read<T>(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Data file not found.", path);
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidDataException($"'{path}' contained no data.");
    }

    private static void ThrowIfInvalid(ForceDataset dataset)
    {
        var errors = ForceDatasetValidator.Validate(dataset);
        if (errors.Count > 0)
        {
            throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        }
    }
}

public static class DataRootLocator
{
    public static string Find(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath) && Directory.Exists(explicitPath))
        {
            return Path.GetFullPath(explicitPath);
        }

        var current = Path.Combine(Directory.GetCurrentDirectory(), "data");
        if (Directory.Exists(current)) return current;

        var output = Path.Combine(AppContext.BaseDirectory, "data");
        if (Directory.Exists(output)) return output;

        throw new DirectoryNotFoundException("Could not find the FieldSim data directory.");
    }
}
