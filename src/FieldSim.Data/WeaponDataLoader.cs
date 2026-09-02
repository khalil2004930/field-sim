using System.Text.Json;
using System.Text.Json.Serialization;
using FieldSim.Domain;

namespace FieldSim.Data;

public static class WeaponDataLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static WeaponDataset LoadHezbollahBaseline(string dataRoot)
    {
        var baseline = Path.Combine(dataRoot, "osint_baseline");
        var sources = Read<List<WeaponSourceRecord>>(
            Path.Combine(baseline, "hezbollah_weapon_sources.json"));
        var weapons = Read<List<WeaponSystem>>(
            Path.Combine(baseline, "hezbollah_weapons.json"));
        var researchQueue = Read<List<WeaponCandidateRecord>>(
            Path.Combine(baseline, "hezbollah_weapon_research_queue.json"));
        var dataset = new WeaponDataset
        {
            Sources = sources,
            Weapons = weapons,
            ResearchQueue = researchQueue
        };
        ThrowIfInvalid(dataset);
        return dataset;
    }

    public static void ApplyOverrides(WeaponDataset dataset, string overridePath)
    {
        foreach (var item in Read<List<WeaponManualOverride>>(overridePath))
        {
            ApplyOverride(dataset, item);
        }
        ThrowIfInvalid(dataset);
    }

    public static void ApplyOverride(WeaponDataset dataset, WeaponManualOverride item)
    {
        var weapon = dataset.FindWeapon(item.WeaponId)
            ?? throw new InvalidDataException($"Unknown weapon '{item.WeaponId}'.");
        var changes = 0;
        changes += Set(item.MinimumRangeKm, value => weapon.Range.MinimumKm = value);
        changes += Set(item.MaximumRangeKm, value => weapon.Range.MaximumKm = value);
        changes += Set(item.CepMeters, value =>
        {
            weapon.Accuracy.Measure = AccuracyMeasure.CircularErrorProbable;
            weapon.Accuracy.CepMeters = value;
            weapon.Accuracy.DispersionMajorMeters = null;
            weapon.Accuracy.DispersionMinorMeters = null;
            weapon.Accuracy.Basis = "Manual scenario override";
        });
        changes += Set(item.BaselineReliabilityPercent,
            value => weapon.BaselineReliabilityPercent = value);
        changes += Set(item.TrainingBurden, value => weapon.Operator.TrainingBurden = value);
        changes += Set(item.SetupBurden, value => weapon.Operator.SetupBurden = value);
        changes += Set(item.LogisticsBurden, value => weapon.Operator.LogisticsBurden = value);
        changes += Set(item.WindSensitivity, value => weapon.Weather.Wind = value);
        changes += Set(item.PrecipitationSensitivity,
            value => weapon.Weather.Precipitation = value);
        changes += Set(item.VisibilitySensitivity, value => weapon.Weather.Visibility = value);
        changes += Set(item.TemperatureSensitivity, value => weapon.Weather.Temperature = value);
        changes += Set(item.NavigationInterferenceSensitivity,
            value => weapon.Weather.NavigationInterference = value);
        weapon.OverrideCount += changes;
        dataset.TotalOverrides += changes;
    }

    private static int Set<T>(T? value, Action<T> setter) where T : struct
    {
        if (value is not T actual) return 0;
        setter(actual);
        return 1;
    }

    private static T Read<T>(string path)
    {
        if (!File.Exists(path)) throw new FileNotFoundException("Data file not found.", path);
        return JsonSerializer.Deserialize<T>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException($"'{path}' contained no data.");
    }

    private static void ThrowIfInvalid(WeaponDataset dataset)
    {
        var errors = WeaponDatasetValidator.Validate(dataset);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
    }
}
