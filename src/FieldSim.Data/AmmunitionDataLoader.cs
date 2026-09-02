using System.Text.Json;
using FieldSim.Domain;

namespace FieldSim.Data;

public static class AmmunitionDataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public static CartridgeFamilyDataset LoadSmallArmsCartridgeFamilies(string dataRoot)
    {
        var path = Path.Combine(dataRoot, "ammunition", "small_arms_cartridge_families_v1_7.json");
        var dataset = JsonSerializer.Deserialize<CartridgeFamilyDataset>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"Could not deserialize cartridge-family dataset '{path}'.");
        var duplicate = dataset.Cartridges.GroupBy(item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
            throw new InvalidDataException($"Duplicate cartridge-family id '{duplicate.Key}'.");
        return dataset;
    }
}
