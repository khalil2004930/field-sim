using System.Text.Json;
using FieldSim.Domain;

namespace FieldSim.Data;

public static class ScenarioPackageLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static ScenarioPackage LoadV17(string dataRoot) =>
        Load(dataRoot, "v1_7_integrated_scenario.json", "FieldSim v1.7 scenario package");

    public static ScenarioPackage LoadCountryScaleV192(string dataRoot) =>
        Load(dataRoot, "v1_9_2_country_scale_scenario.json", "FieldSim v1.9.2 country-scale scenario package");

    public static ScenarioPackage LoadCountryScaleV193(string dataRoot) =>
        Load(dataRoot, "v1_9_3_bint_jbeil_scenario.json", "FieldSim v1.9.3 Bint Jbeil scenario package");

    public static ScenarioPackage LoadCountryScaleV110(string dataRoot) =>
        Load(dataRoot, "v1_10_urban_c2_scenario.json", "FieldSim v1.10 urban C2 scenario package");

    private static ScenarioPackage Load(string dataRoot, string fileName, string description)
    {
        var path = Path.Combine(dataRoot, "scenarios", fileName);
        if (!File.Exists(path)) throw new FileNotFoundException($"{description} was not found.", path);
        var package = JsonSerializer.Deserialize<ScenarioPackage>(File.ReadAllText(path), Options)
            ?? throw new InvalidDataException($"Could not deserialize the {description}.");
        Validate(package);
        return package;
    }

    public static void Validate(ScenarioPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (string.IsNullOrWhiteSpace(package.Id))
            throw new InvalidDataException("Scenario package id is required.");
        if (string.IsNullOrWhiteSpace(package.VillageId))
            throw new InvalidDataException("Scenario package village id is required.");
        if (string.IsNullOrWhiteSpace(package.OrbatFile) ||
            !string.Equals(Path.GetFileName(package.OrbatFile), package.OrbatFile, StringComparison.Ordinal) ||
            !package.OrbatFile.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Scenario package ORBAT reference must be a JSON filename inside data/scenarios.");

        if (!double.IsFinite(package.DisplayMap.CenterLatitude) ||
            !double.IsFinite(package.DisplayMap.CenterLongitude) ||
            !double.IsFinite(package.DisplayMap.TheaterWidthMeters) ||
            !double.IsFinite(package.DisplayMap.TheaterHeightMeters) ||
            package.DisplayMap.TheaterWidthMeters < 0 || package.DisplayMap.TheaterHeightMeters < 0)
            throw new InvalidDataException("Scenario display-map dimensions and center must be finite and non-negative.");

        var usesCountryScaleAuthoring = package.GeoAnchors.Count > 0 || package.PlacementZones.Count > 0 ||
                                       package.Objectives.Count > 0 || package.SupportPlacements.Count > 0;
        if (usesCountryScaleAuthoring &&
            (package.DisplayMap.TheaterWidthMeters <= 0 || package.DisplayMap.TheaterHeightMeters <= 0))
            throw new InvalidDataException("Country-scale scenario authoring requires positive theater dimensions.");

        foreach (var assignment in package.EntityAssignments)
        {
            if (string.IsNullOrWhiteSpace(assignment.EntityKey))
                throw new InvalidDataException("Every scenario entity assignment requires a stable entity key.");
            if (string.IsNullOrWhiteSpace(assignment.OrbatNodeId))
                throw new InvalidDataException($"Scenario entity '{assignment.EntityKey}' requires an ORBAT node id.");

            var placement = assignment.InitialPlacement;
            if (placement is null) continue;
            if (!double.IsFinite(placement.XMeters) || !double.IsFinite(placement.YMeters) ||
                !double.IsFinite(placement.OffsetEastMeters) || !double.IsFinite(placement.OffsetNorthMeters) ||
                !double.IsFinite(placement.HeadingDegrees))
                throw new InvalidDataException($"Initial placement for '{assignment.EntityKey}' contains a non-finite value.");
        }

        var duplicateEntities = package.EntityAssignments
            .GroupBy(item => item.EntityKey, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        if (duplicateEntities.Length > 0)
            throw new InvalidDataException($"Duplicate scenario entity keys: {string.Join(", ", duplicateEntities)}");

        ValidateCountryScaleAuthoring(package);
    }

    private static void ValidateCountryScaleAuthoring(ScenarioPackage package)
    {
        var duplicateAnchors = package.GeoAnchors.GroupBy(item => item.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicateAnchors.Length > 0)
            throw new InvalidDataException($"Duplicate scenario geo-anchor ids: {string.Join(", ", duplicateAnchors)}");

        var duplicateZones = package.PlacementZones.GroupBy(item => item.Id, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicateZones.Length > 0)
            throw new InvalidDataException($"Duplicate scenario placement-zone ids: {string.Join(", ", duplicateZones)}");

        var duplicateSupport = package.SupportPlacements.GroupBy(item => item.AssetId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1).Select(group => group.Key).ToArray();
        if (duplicateSupport.Length > 0)
            throw new InvalidDataException($"Duplicate support-placement ids: {string.Join(", ", duplicateSupport)}");

        var anchors = package.GeoAnchors.ToDictionary(item => item.Id, StringComparer.Ordinal);
        var zones = package.PlacementZones.ToDictionary(item => item.Id, StringComparer.Ordinal);

        foreach (var anchor in package.GeoAnchors)
        {
            if (string.IsNullOrWhiteSpace(anchor.Id) || string.IsNullOrWhiteSpace(anchor.DisplayName) ||
                !double.IsFinite(anchor.Latitude) || !double.IsFinite(anchor.Longitude))
                throw new InvalidDataException("Every geo anchor requires a finite latitude/longitude, id and display name.");

            var projected = ScenarioGeoProjection.ProjectToLocal(package.DisplayMap, anchor.Latitude, anchor.Longitude);
            if (!InsideTheater(package, projected))
                throw new InvalidDataException($"Geo anchor '{anchor.Id}' projects outside the simulation theater.");
        }

        foreach (var zone in package.PlacementZones)
        {
            if (string.IsNullOrWhiteSpace(zone.Id) || string.IsNullOrWhiteSpace(zone.AnchorId))
                throw new InvalidDataException("Every placement zone requires an id and anchor id.");
            if (!anchors.ContainsKey(zone.AnchorId))
                throw new InvalidDataException($"Placement zone '{zone.Id}' references missing anchor '{zone.AnchorId}'.");
            if (!double.IsFinite(zone.OffsetEastMeters) || !double.IsFinite(zone.OffsetNorthMeters) ||
                !double.IsFinite(zone.RadiusMeters) || zone.RadiusMeters <= 0)
                throw new InvalidDataException($"Placement zone '{zone.Id}' contains invalid offsets or radius.");

            var anchor = anchors[zone.AnchorId];
            var anchorPoint = ScenarioGeoProjection.ProjectToLocal(package.DisplayMap, anchor.Latitude, anchor.Longitude);
            var center = new ScenarioLocalPoint(anchorPoint.X + zone.OffsetEastMeters, anchorPoint.Y + zone.OffsetNorthMeters);
            if (!InsideTheater(package, center))
                throw new InvalidDataException($"Placement zone '{zone.Id}' is centered outside the simulation theater.");
        }

        foreach (var assignment in package.EntityAssignments.Where(item => item.InitialPlacement is not null))
        {
            var placement = assignment.InitialPlacement!;
            if (string.IsNullOrWhiteSpace(placement.ZoneId)) continue;
            if (!zones.TryGetValue(placement.ZoneId, out var zone))
                throw new InvalidDataException($"Entity '{assignment.EntityKey}' references missing placement zone '{placement.ZoneId}'.");
            var offsetDistance = Math.Sqrt(placement.OffsetEastMeters * placement.OffsetEastMeters +
                                           placement.OffsetNorthMeters * placement.OffsetNorthMeters);
            if (offsetDistance > zone.RadiusMeters)
                throw new InvalidDataException($"Entity '{assignment.EntityKey}' is authored outside placement zone '{zone.Id}'.");
        }

        foreach (var objective in package.Objectives)
        {
            if (string.IsNullOrWhiteSpace(objective.Id) || string.IsNullOrWhiteSpace(objective.ZoneId))
                throw new InvalidDataException("Every objective requires an id and placement zone.");
            if (!zones.TryGetValue(objective.ZoneId, out var zone))
                throw new InvalidDataException($"Objective '{objective.Id}' references missing placement zone '{objective.ZoneId}'.");
            if (!double.IsFinite(objective.CaptureRadiusMeters) || objective.CaptureRadiusMeters <= 0 ||
                objective.RequiredControlSeconds <= 0)
                throw new InvalidDataException($"Objective '{objective.Id}' has invalid capture/control settings.");
            var offsetDistance = Math.Sqrt(objective.OffsetEastMeters * objective.OffsetEastMeters +
                                           objective.OffsetNorthMeters * objective.OffsetNorthMeters);
            if (offsetDistance > zone.RadiusMeters)
                throw new InvalidDataException($"Objective '{objective.Id}' is authored outside placement zone '{zone.Id}'.");
        }

        foreach (var placement in package.SupportPlacements)
        {
            if (string.IsNullOrWhiteSpace(placement.AssetId) || string.IsNullOrWhiteSpace(placement.ZoneId))
                throw new InvalidDataException("Every support placement requires an asset id and placement zone.");
            if (!zones.TryGetValue(placement.ZoneId, out var zone))
                throw new InvalidDataException($"Support asset '{placement.AssetId}' references missing placement zone '{placement.ZoneId}'.");
            if (!double.IsFinite(placement.OffsetEastMeters) || !double.IsFinite(placement.OffsetNorthMeters) ||
                !double.IsFinite(placement.AltitudeMeters) || placement.AltitudeMeters < 0)
                throw new InvalidDataException($"Support placement '{placement.AssetId}' contains invalid values.");
            var offsetDistance = Math.Sqrt(placement.OffsetEastMeters * placement.OffsetEastMeters +
                                           placement.OffsetNorthMeters * placement.OffsetNorthMeters);
            if (offsetDistance > zone.RadiusMeters)
                throw new InvalidDataException($"Support asset '{placement.AssetId}' is authored outside placement zone '{zone.Id}'.");
        }
    }

    private static bool InsideTheater(ScenarioPackage package, ScenarioLocalPoint point) =>
        point.X >= 0 && point.Y >= 0 &&
        point.X < package.DisplayMap.TheaterWidthMeters &&
        point.Y < package.DisplayMap.TheaterHeightMeters;
}
