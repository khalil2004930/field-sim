using System.Text.Json;
using System.Text.Json.Serialization;
using FieldSim.Domain;

namespace FieldSim.Data;

public static class ScenarioOrbatDataLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static ScenarioOrbatDataset LoadPrototype(string dataRoot)
        => Load(dataRoot, "orbat_nato_prototype.json");

    public static ScenarioOrbatDataset Load(string dataRoot, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal) ||
            !fileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Scenario ORBAT file must be a JSON filename inside data/scenarios.");
        var path = Path.Combine(dataRoot, "scenarios", fileName);
        if (!File.Exists(path)) throw new FileNotFoundException("Scenario ORBAT dataset not found.", path);
        var dataset = JsonSerializer.Deserialize<ScenarioOrbatDataset>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"'{path}' contained no ORBAT data.");
        var errors = ScenarioOrbatDataset.Validate(dataset);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return dataset;
    }
}
