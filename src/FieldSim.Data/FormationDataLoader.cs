using System.Text.Json;
using System.Text.Json.Serialization;
using FieldSim.Domain;

namespace FieldSim.Data;

public static class FormationDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static FormationDataset LoadIdfPublicPeacetime(string dataRoot)
    {
        var path = Path.Combine(dataRoot, "organizations", "idf_public_peacetime.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Formation dataset not found.", path);
        var dataset = JsonSerializer.Deserialize<FormationDataset>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"'{path}' contained no formation data.");
        var errors = FormationDatasetValidator.Validate(dataset);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return dataset;
    }
}
