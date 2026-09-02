using System.Text.Json;
using FieldSim.Domain;

namespace FieldSim.Data;

public static class SmallArmsDataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static SmallArmsOsintDatabase LoadHezbollahSmallArms(string dataRoot)
    {
        var path = Path.Combine(dataRoot, "osint_baseline", "hezbollah_small_arms_osint_v1_0.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Hezbollah small-arms OSINT dataset was not found.", path);
        var json = File.ReadAllText(path);
        var dataset = JsonSerializer.Deserialize<SmallArmsOsintDatabase>(json, Options)
            ?? throw new InvalidDataException("Could not deserialize Hezbollah small-arms OSINT dataset.");
        Validate(dataset);
        return dataset;
    }

    public static void Validate(SmallArmsOsintDatabase dataset)
    {
        ArgumentNullException.ThrowIfNull(dataset);
        var duplicates = dataset.Weapons
            .GroupBy(item => item.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicates.Length > 0)
            throw new InvalidDataException($"Duplicate small-arm IDs: {string.Join(", ", duplicates)}");
        if (dataset.Weapons.Any(item => string.IsNullOrWhiteSpace(item.Caliber)))
            throw new InvalidDataException("Every small-arm record must explicitly state a caliber/chambering field, including unresolved values.");
    }
}
