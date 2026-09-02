using System.Text.Json;
using System.Text.Json.Serialization;
using FieldSim.Core;

namespace FieldSim.Data;

public static class SupportDataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static SupportAssetCatalog LoadV19Catalog(string dataRoot)
    {
        var path = Path.Combine(dataRoot, "scenarios", "v1_8_support_asset_catalog.json");
        if (!File.Exists(path)) throw new FileNotFoundException("FieldSim v1.9 support catalog was not found.", path);
        var catalog = JsonSerializer.Deserialize<SupportAssetCatalog>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException("Could not deserialize the FieldSim v1.9 support catalog.");
        var duplicateIds = catalog.Assets.GroupBy(item => item.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicateIds.Length > 0)
            throw new InvalidDataException($"Duplicate support catalog ids: {string.Join(", ", duplicateIds)}");
        if (catalog.Assets.Any(item => item.ScenarioQuantity < 0))
            throw new InvalidDataException("Support catalog quantities cannot be negative.");
        return catalog;
    }
}
