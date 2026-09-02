using System.Text.Json;
using System.Text.Json.Serialization;
using FieldSim.Domain;

namespace FieldSim.Data;

public static class VehicleDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static VehicleDataset LoadIdfGroundVehicleBaseline(string dataRoot)
    {
        var path = Path.Combine(dataRoot, "vehicles", "idf_ground_vehicle_baseline.json");
        if (!File.Exists(path)) throw new FileNotFoundException("Vehicle dataset not found.", path);
        var dataset = JsonSerializer.Deserialize<VehicleDataset>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"'{path}' contained no vehicle data.");
        var errors = VehicleDatasetValidator.Validate(dataset);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return dataset;
    }
}
