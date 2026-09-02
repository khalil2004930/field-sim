using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using System.Windows.Threading;
using FieldSim.Core;
using FieldSim.Data;
using FieldSim.Domain;
using FieldSim.Scenarios;

namespace FieldSim.Desktop;

public partial class MainWindow : Window
{
    private const double CellSize = 50;
    private const double TheaterCanvasSize = 800;
    private readonly DispatcherTimer _timer;
    private readonly IReadOnlyList<VillageSectorDefinition> _sectors = VillageMapCatalog.CreateDefault();
    private FormationDataset? _formationDataset;
    private ScenarioOrbatDataset? _scenarioOrbat;
    private readonly Dictionary<int, string> _orbatBindings = [];
    private VehicleDataset? _vehicleDataset;
    private TheaterMapPackage? _theaterMap;
    private GeoBounds _theaterViewport = new(33.65, 29.35, 36.75, 34.85);
    private VillageMapDefinition _activeVillage;
    private TacticalState _state;
    private int _selectedUnitIndex = -1;
    private GridPoint? _selectedGridCell;
    private int? _selectedKeypad;
    private bool _running;
    private bool _initialized;
    private bool _updatingMapSelectors;
    private string? _selectedOrbatNodeId;

    public MainWindow()
    {
        _activeVillage = _sectors[0].Villages[0];
        _state = VillageTrainingScenario.Create(_activeVillage);
        InitializeComponent();

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(500)
        };
        _timer.Tick += (_, _) => AdvanceOneTick();

        PopulateSectorSelector();
        SectorBox.SelectedIndex = 0;
        PopulateVillageSelector();
        VillageBox.SelectedIndex = 0;
        InitializeReferenceData();
        InitializeTheaterMap();

        _initialized = true;
        LoadInitialBattleEvents();
        RenderAll();
        RenderTheaterMap();
    }

    private void InitializeTheaterMap()
    {
        try
        {
            _theaterMap = TheaterMapLoader.LoadDefault(DataRootLocator.Find());
            _theaterViewport = _theaterMap.Manifest.Bounds;
            TheaterAttributionText.Text = string.Join(" • ", _theaterMap.Manifest.Attributions.Select(source =>
                $"{source.Name} ({source.License})"));
            if (_theaterMap.MissingOptionalLayers.Count > 0)
                TheaterAttributionText.Text += " • Optional detailed layers not built: " +
                    string.Join(", ", _theaterMap.MissingOptionalLayers);
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            TheaterAttributionText.Text = "Theater map unavailable: " + exception.Message;
        }
    }

    private void InitializeReferenceData()
    {
        try
        {
            var dataRoot = DataRootLocator.Find();
            var formationDataset = FormationDataLoader.LoadIdfPublicPeacetime(dataRoot);
            var scenarioOrbat = ScenarioOrbatDataLoader.LoadPrototype(dataRoot);
            var vehicleDataset = VehicleDataLoader.LoadIdfGroundVehicleBaseline(dataRoot);
            _formationDataset = formationDataset;
            _scenarioOrbat = scenarioOrbat;
            _vehicleDataset = vehicleDataset;
            PopulateOrbatTrees();
            BindTacticalUnitsToOrbat();
            PopulateFormationTree();
            VehicleList.ItemsSource = vehicleDataset.Vehicles;
            if (vehicleDataset.Vehicles.Count > 0) VehicleList.SelectedIndex = 0;
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or ArgumentException)
        {
            FormationMetaText.Text = "Reference data unavailable: " + exception.Message;
            OrbatScenarioText.Text = "ORBAT unavailable: " + exception.Message;
            VehicleStatusText.Text = "Reference data unavailable: " + exception.Message;
        }
    }

    private void StartPause_Click(object sender, RoutedEventArgs e)
    {
        if (_state.Result != BattleResult.Ongoing)
        {
            StatusText.Text = "The engagement is complete. Press Reset to run it again.";
            return;
        }
        _running = !_running;
        if (_running)
        {
            _timer.Start();
            AddEvent("Simulation started.");
        }
        else
        {
            _timer.Stop();
            AddEvent("Simulation paused.");
        }
        UpdateSimulationControls();
    }

    private void Step_Click(object sender, RoutedEventArgs e)
    {
        if (_running)
        {
            _running = false;
            _timer.Stop();
        }
        AdvanceOneTick();
        UpdateSimulationControls();
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        _running = false;
        _timer.Stop();
        _state = VillageTrainingScenario.Create(_activeVillage);
        BindTacticalUnitsToOrbat();
        ApplyEnvironmentSelection();
        _selectedUnitIndex = -1;
        _selectedGridCell = null;
        _selectedKeypad = null;
        EventList.Items.Clear();
        LoadInitialBattleEvents();
        AddEvent("[SYSTEM] Scenario reset; autonomous orders restored.");
        RenderAll();
    }

    private void SpeedBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_timer is null || SpeedBox.SelectedItem is not ComboBoxItem item ||
            item.Tag is not string millisecondsText ||
            !double.TryParse(millisecondsText, NumberStyles.Number,
                CultureInfo.InvariantCulture, out var milliseconds))
        {
            return;
        }
        _timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
    }

    private void PerspectiveBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initialized) RenderAll();
    }

    private void DisplayOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_initialized) RenderAll();
    }

    private void EnvironmentBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        ApplyEnvironmentSelection();
        AddEvent($"Environment changed: {_state.Environment.Light} / {_state.Environment.Visibility}.");
        DetectionEngine.Update(_state);
        RenderAll();
    }

    private void ApplyEnvironmentSelection()
    {
        var light = LightCondition.Day;
        var visibility = WeatherVisibility.Clear;
        if (LightBox?.SelectedItem is ComboBoxItem lightItem && lightItem.Tag is string lightText)
            Enum.TryParse(lightText, true, out light);
        if (WeatherBox?.SelectedItem is ComboBoxItem weatherItem && weatherItem.Tag is string weatherText)
            Enum.TryParse(weatherText, true, out visibility);
        _state.Environment.ApplyPreset(light, visibility);
    }

    private TacticalFaction PerspectiveFaction()
    {
        if (PerspectiveBox.SelectedItem is ComboBoxItem item && item.Tag is string text &&
            Enum.TryParse<TacticalFaction>(text, true, out var faction))
        {
            return faction;
        }
        return TacticalFaction.Blue;
    }

    private void SectorBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _updatingMapSelectors) return;
        _updatingMapSelectors = true;
        PopulateVillageSelector();
        VillageBox.SelectedIndex = 0;
        _updatingMapSelectors = false;
        LoadSelectedVillage();
    }

    private void VillageBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized || _updatingMapSelectors) return;
        LoadSelectedVillage();
    }

    private void PopulateSectorSelector()
    {
        SectorBox.ItemsSource = _sectors.Select(sector => sector.Name).ToArray();
    }

    private void PopulateVillageSelector()
    {
        var sectorIndex = Math.Clamp(SectorBox.SelectedIndex, 0, _sectors.Count - 1);
        VillageBox.ItemsSource = _sectors[sectorIndex].Villages
            .Select(village => village.Name)
            .ToArray();
    }

    private void LoadSelectedVillage()
    {
        var sectorIndex = Math.Clamp(SectorBox.SelectedIndex, 0, _sectors.Count - 1);
        var villages = _sectors[sectorIndex].Villages;
        var villageIndex = Math.Clamp(VillageBox.SelectedIndex, 0, villages.Count - 1);
        _activeVillage = villages[villageIndex];
        _state = VillageTrainingScenario.Create(_activeVillage);
        BindTacticalUnitsToOrbat();
        ApplyEnvironmentSelection();
        _selectedUnitIndex = -1;
        _selectedGridCell = null;
        _selectedKeypad = null;
        _running = false;
        _timer.Stop();
        EventList.Items.Clear();
        LoadInitialBattleEvents();
        StatusText.Text = $"{_activeVillage.Name} ready — autonomous orders loaded; press Start.";
        RenderAll();
    }

    private void MapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var cell = CellFromMouse(e);
        var keypad = KeypadFromMouse(e, cell);
        SelectGridReference(cell, keypad);
        var candidate = TacticalEngine.SelectAt(_state, cell.X, cell.Y, null);
        if (candidate >= 0 && FogToggle.IsChecked == true &&
            !UnitVisibleToPerspective(_state.Units[candidate]))
        {
            candidate = -1;
        }
        _selectedUnitIndex = candidate;

        if (_selectedUnitIndex >= 0)
        {
            var unit = _state.Units[_selectedUnitIndex];
            StatusText.Text = $"{unit.DisplayName} selected at {VillageGridReference.FullLabel(cell, keypad)}.";
            if (_scenarioOrbat is not null && _orbatBindings.TryGetValue(unit.Id, out var nodeId))
            {
                var node = _scenarioOrbat.Find(nodeId);
                if (node is not null) RenderOrbatNode(node);
            }
        }
        else
        {
            StatusText.Text = $"Grid reference {VillageGridReference.FullLabel(cell, keypad)} selected.";
        }
        RenderAll();
    }

    private void MapCanvas_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var destination = CellFromMouse(e);
        var keypad = KeypadFromMouse(e, destination);
        SelectGridReference(destination, keypad);

        if (_selectedUnitIndex < 0)
        {
            StatusText.Text = "Select a visible unit before issuing an order.";
            RenderAll();
            return;
        }

        var unit = _state.Units[_selectedUnitIndex];
        if (TacticalEngine.IssueMove(_state, _selectedUnitIndex, destination))
        {
            TacticalAiEngine.AssignOrder(_state, unit, TacticalOrderType.Advance, destination);
            _state.AddActivity(TacticalEventType.Order,
                $"OBSERVER OVERRIDE: {unit.DisplayName} ordered to {VillageGridReference.CellLabel(destination)}.",
                unit.Faction, unit.Id);
            AddEvent($"[ORDER] {unit.DisplayName} → {VillageGridReference.CellLabel(destination)} ({unit.Path.Count} cells). ");
            StatusText.Text = $"Movement order accepted for {VillageGridReference.FullLabel(destination, keypad)}.";
        }
        else
        {
            AddEvent($"{unit.DisplayName} order rejected at {VillageGridReference.CellLabel(destination)}.");
            StatusText.Text = "Order rejected — choose a walkable, unoccupied destination.";
        }
        RenderAll();
    }

    private GridPoint CellFromMouse(MouseButtonEventArgs e)
    {
        var position = e.GetPosition(MapCanvas);
        var x = Math.Clamp((int)(position.X / CellSize), 0, _state.Width - 1);
        var y = Math.Clamp((int)(position.Y / CellSize), 0, _state.Height - 1);
        return new GridPoint(x, y);
    }

    private int KeypadFromMouse(MouseButtonEventArgs e, GridPoint cell)
    {
        var position = e.GetPosition(MapCanvas);
        var normalizedX = (position.X - cell.X * CellSize) / CellSize;
        var normalizedY = (position.Y - cell.Y * CellSize) / CellSize;
        return VillageGridReference.KeypadFromNormalizedPosition(normalizedX, normalizedY);
    }

    private void SelectGridReference(GridPoint cell, int keypad)
    {
        _selectedGridCell = cell;
        _selectedKeypad = keypad;
        GridReferenceText.Text = VillageGridReference.CellLabel(cell);
        KeypadRefText.Text = $"KP{keypad}  ({VillageGridReference.FullLabel(cell, keypad)})";
    }

    private void AdvanceOneTick()
    {
        TacticalEngine.Step(_state);
        foreach (var activity in _state.ActivityEvents.Where(item => item.Tick == _state.Tick))
            AddEvent($"[{activity.Type.ToString().ToUpperInvariant()}] {activity.Message}");
        foreach (var combatEvent in _state.CombatEvents.Where(item => item.Tick == _state.Tick))
            AddEvent($"[{combatEvent.Type.ToString().ToUpperInvariant()}] {combatEvent.Message}");
        if (_state.Result != BattleResult.Ongoing)
        {
            _running = false;
            _timer.Stop();
        }
        RenderAll();
    }

    private void RenderAll()
    {
        RenderMap();
        if (MapModeTabs.SelectedIndex == 1) Render3DView();
        UpdateLocationPanel();
        UpdateInspector();
        UpdateBattleHud();
        UpdateRecentAction();
        UpdateOrbatLiveSummary();
        UpdateSimulationControls();
    }

    private void MapModeTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_initialized) return;
        if (MapModeTabs.SelectedIndex == 1) Render3DView();
        else if (MapModeTabs.SelectedIndex == 2) RenderTheaterMap();
    }

    private void TheaterFullExtent_Click(object sender, RoutedEventArgs e)
    {
        if (_theaterMap is null) return;
        _theaterViewport = _theaterMap.Manifest.Bounds;
        RenderTheaterMap();
    }

    private void TheaterSouthLebanon_Click(object sender, RoutedEventArgs e)
    {
        _theaterViewport = new GeoBounds(34.92, 32.90, 35.80, 33.68);
        RenderTheaterMap();
    }

    private void TheaterMapCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_theaterMap is null) return;
        var pointer = e.GetPosition(TheaterMapCanvas);
        var anchor = WebMercator.FromCanvas(new ProjectedPoint(pointer.X, pointer.Y),
            _theaterViewport, TheaterCanvasSize, TheaterCanvasSize);
        var factor = e.Delta > 0 ? 0.72 : 1.38;
        var longitudeSpan = Math.Clamp(_theaterViewport.LongitudeSpan * factor, 0.06,
            _theaterMap.Manifest.Bounds.LongitudeSpan);
        var latitudeSpan = Math.Clamp(_theaterViewport.LatitudeSpan * factor, 0.06,
            _theaterMap.Manifest.Bounds.LatitudeSpan);
        var xFraction = Math.Clamp(pointer.X / TheaterCanvasSize, 0, 1);
        var yFraction = Math.Clamp(pointer.Y / TheaterCanvasSize, 0, 1);
        var candidate = new GeoBounds(
            anchor.Longitude - longitudeSpan * xFraction,
            anchor.Latitude - latitudeSpan * (1 - yFraction),
            anchor.Longitude + longitudeSpan * (1 - xFraction),
            anchor.Latitude + latitudeSpan * yFraction);
        _theaterViewport = ClampTheaterViewport(candidate);
        RenderTheaterMap();
        e.Handled = true;
    }

    private void TheaterMapCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_theaterMap is null) return;
        var pointer = e.GetPosition(TheaterMapCanvas);
        var coordinate = WebMercator.FromCanvas(new ProjectedPoint(pointer.X, pointer.Y),
            _theaterViewport, TheaterCanvasSize, TheaterCanvasSize);
        var nearest = NearestReferencePlace(coordinate);
        var nearestText = "";
        if (nearest is not null)
        {
            var place = nearest.Value.Feature;
            var distance = nearest.Value.DistanceKilometers;
            if (distance <= Math.Max(4, _theaterViewport.LatitudeSpan * 18))
            {
                var name = place.Properties.GetValueOrDefault("name", "place");
                nearestText = $" • nearest {name} ({distance:F1} km)";
                if (e.ClickCount == 2 && distance <= 8 &&
                    place.Properties.TryGetValue("villageId", out var villageId))
                {
                    SelectVillageFromTheater(villageId);
                    return;
                }
                if (place.Properties.ContainsKey("villageId")) nearestText += " • double-click to open local grid";
            }
        }
        TheaterCoordinateText.Text =
            $"{coordinate.Latitude:F5}°, {coordinate.Longitude:F5}°{nearestText}";
    }

    private void SelectVillageFromTheater(string villageId)
    {
        for (var sectorIndex = 0; sectorIndex < _sectors.Count; sectorIndex++)
        {
            var villageIndex = _sectors[sectorIndex].Villages.ToList()
                .FindIndex(village => village.Id.Equals(villageId, StringComparison.OrdinalIgnoreCase));
            if (villageIndex < 0) continue;
            _updatingMapSelectors = true;
            SectorBox.SelectedIndex = sectorIndex;
            PopulateVillageSelector();
            VillageBox.SelectedIndex = villageIndex;
            _updatingMapSelectors = false;
            LoadSelectedVillage();
            MapModeTabs.SelectedIndex = 0;
            StatusText.Text = $"Opened {_activeVillage.Name}'s synthetic local tactical grid from its public map label.";
            return;
        }
    }

    private (MapFeature Feature, double DistanceKilometers)? NearestReferencePlace(GeoCoordinate coordinate)
    {
        if (_theaterMap is null || !_theaterMap.Layers.TryGetValue("reference-places", out var places)) return null;
        var candidates = places
            .Where(feature => feature.GeometryType == MapGeometryType.Point && feature.Parts.Count > 0 && feature.Parts[0].Count > 0)
            .Select(feature => (Feature: feature,
                DistanceKilometers: GeoMath.DistanceKilometers(coordinate, feature.Parts[0][0])))
            .OrderBy(item => item.DistanceKilometers)
            .Take(1)
            .ToArray();
        return candidates.Length == 0 ? null : candidates[0];
    }

    private GeoBounds ClampTheaterViewport(GeoBounds candidate)
    {
        var limit = _theaterMap!.Manifest.Bounds;
        var longitudeSpan = Math.Min(candidate.LongitudeSpan, limit.LongitudeSpan);
        var latitudeSpan = Math.Min(candidate.LatitudeSpan, limit.LatitudeSpan);
        var west = Math.Clamp(candidate.West, limit.West, limit.East - longitudeSpan);
        var south = Math.Clamp(candidate.South, limit.South, limit.North - latitudeSpan);
        return new GeoBounds(west, south, west + longitudeSpan, south + latitudeSpan);
    }

    private void RenderTheaterMap()
    {
        TheaterMapCanvas.Children.Clear();
        if (_theaterMap is null) return;
        DrawTheaterGraticule();
        foreach (var descriptor in _theaterMap.Manifest.Layers.OrderBy(descriptor => TheaterLayerOrder(descriptor.Style)))
        {
            if (!_theaterMap.Layers.TryGetValue(descriptor.Id, out var features)) continue;
            foreach (var feature in features) DrawTheaterFeature(descriptor.Style, feature);
        }
        DrawTheaterScaleBar();
    }

    private void DrawTheaterGraticule()
    {
        var interval = _theaterViewport.LatitudeSpan switch
        {
            < 0.3 => 0.05,
            < 1.0 => 0.2,
            < 3.0 => 0.5,
            _ => 1.0
        };
        for (var longitude = Math.Ceiling(_theaterViewport.West / interval) * interval;
             longitude < _theaterViewport.East; longitude += interval)
        {
            var top = TheaterPoint(new GeoCoordinate(_theaterViewport.North, longitude));
            var bottom = TheaterPoint(new GeoCoordinate(_theaterViewport.South, longitude));
            TheaterMapCanvas.Children.Add(new Line { X1 = top.X, Y1 = top.Y, X2 = bottom.X, Y2 = bottom.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(45, 198, 216, 225)), StrokeThickness = 0.7,
                IsHitTestVisible = false });
        }
        for (var latitude = Math.Ceiling(_theaterViewport.South / interval) * interval;
             latitude < _theaterViewport.North; latitude += interval)
        {
            var left = TheaterPoint(new GeoCoordinate(latitude, _theaterViewport.West));
            var right = TheaterPoint(new GeoCoordinate(latitude, _theaterViewport.East));
            TheaterMapCanvas.Children.Add(new Line { X1 = left.X, Y1 = left.Y, X2 = right.X, Y2 = right.Y,
                Stroke = new SolidColorBrush(Color.FromArgb(45, 198, 216, 225)), StrokeThickness = 0.7,
                IsHitTestVisible = false });
        }
    }

    private void DrawTheaterFeature(string style, MapFeature feature)
    {
        if (feature.GeometryType == MapGeometryType.Point)
        {
            DrawTheaterPoint(style, feature);
            return;
        }
        foreach (var part in feature.Parts.Where(part => part.Count >= 2))
        {
            var points = new PointCollection(part.Select(coordinate => TheaterPoint(coordinate)));
            if (feature.GeometryType == MapGeometryType.Polygon)
            {
                var lebanon = style == "boundary-lebanon";
                var water = style == "water";
                var polygon = new Polygon
                {
                    Points = points,
                    Fill = new SolidColorBrush(water ? Color.FromArgb(145, 38, 104, 143) :
                        lebanon ? Color.FromArgb(72, 63, 122, 87) : Color.FromArgb(62, 90, 111, 133)),
                    Stroke = new SolidColorBrush(water ? Color.FromRgb(67, 148, 190) :
                        lebanon ? Color.FromRgb(110, 201, 139) : Color.FromRgb(145, 174, 201)),
                    StrokeThickness = water ? 1.0 : 1.6,
                    IsHitTestVisible = false
                };
                TheaterMapCanvas.Children.Add(polygon);
            }
            else
            {
                var water = style == "water";
                var contour = style == "contours";
                TheaterMapCanvas.Children.Add(new Polyline
                {
                    Points = points,
                    Stroke = new SolidColorBrush(water ? Color.FromRgb(67, 148, 190) :
                        contour ? Color.FromRgb(126, 145, 137) : Color.FromRgb(184, 153, 112)),
                    StrokeThickness = water ? 1.4 : contour ? 0.55 : 0.8,
                    Opacity = contour ? 0.55 : 0.82,
                    IsHitTestVisible = false
                });
            }
        }
    }

    private void DrawTheaterPoint(string style, MapFeature feature)
    {
        if (feature.Parts.Count == 0 || feature.Parts[0].Count == 0) return;
        var coordinate = feature.Parts[0][0];
        if (!_theaterViewport.Contains(coordinate)) return;
        var name = feature.Properties.GetValueOrDefault("name", "");
        var placeClass = feature.Properties.GetValueOrDefault("class",
            feature.Properties.GetValueOrDefault("place", "place"));
        var isVillage = placeClass == "village";
        if (isVillage && _theaterViewport.LatitudeSpan > 1.4) return;
        var point = TheaterPoint(coordinate);
        var radius = isVillage ? 3.2 : 4.2;
        var marker = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Fill = new SolidColorBrush(isVillage ? Color.FromRgb(241, 190, 82) : Color.FromRgb(232, 238, 241)),
            Stroke = new SolidColorBrush(Color.FromRgb(18, 32, 42)),
            StrokeThickness = 1.2,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(marker, point.X - radius);
        Canvas.SetTop(marker, point.Y - radius);
        TheaterMapCanvas.Children.Add(marker);
        if (string.IsNullOrWhiteSpace(name)) return;
        var label = new TextBlock
        {
            Text = name,
            Foreground = new SolidColorBrush(Color.FromRgb(235, 242, 245)),
            FontSize = isVillage ? 9 : 10,
            FontWeight = isVillage ? FontWeights.Normal : FontWeights.SemiBold,
            Background = new SolidColorBrush(Color.FromArgb(120, 11, 23, 32)),
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, point.X + radius + 3);
        Canvas.SetTop(label, point.Y - 8);
        TheaterMapCanvas.Children.Add(label);
    }

    private void DrawTheaterScaleBar()
    {
        var centerLatitude = _theaterViewport.Center.Latitude * Math.PI / 180.0;
        var widthKilometers = _theaterViewport.LongitudeSpan * 111.32 * Math.Cos(centerLatitude);
        var candidates = new[] { 1, 2, 5, 10, 20, 50, 100, 200 };
        var kilometers = candidates.LastOrDefault(value => value <= widthKilometers / 4.0);
        if (kilometers == 0) kilometers = 1;
        var pixels = kilometers / widthKilometers * TheaterCanvasSize;
        var line = new Line { X1 = 28, Y1 = TheaterCanvasSize - 28, X2 = 28 + pixels, Y2 = TheaterCanvasSize - 28,
            Stroke = Brushes.White, StrokeThickness = 3, IsHitTestVisible = false };
        TheaterMapCanvas.Children.Add(line);
        var label = new TextBlock { Text = $"{kilometers} km", Foreground = Brushes.White,
            Background = new SolidColorBrush(Color.FromArgb(150, 8, 16, 23)), FontSize = 10, IsHitTestVisible = false };
        Canvas.SetLeft(label, 28);
        Canvas.SetTop(label, TheaterCanvasSize - 48);
        TheaterMapCanvas.Children.Add(label);
    }

    private Point TheaterPoint(GeoCoordinate coordinate)
    {
        var projected = WebMercator.ToCanvas(coordinate, _theaterViewport,
            TheaterCanvasSize, TheaterCanvasSize);
        return new Point(projected.X, projected.Y);
    }

    private static int TheaterLayerOrder(string style) => style switch
    {
        "boundary-lebanon" or "boundary-israel" => 0,
        "water" => 1,
        "contours" => 2,
        "roads" => 3,
        "places" => 4,
        _ => 2
    };

    private void Render3DView()
    {
        Tactical3DViewport.Children.Clear();

        var camera = new PerspectiveCamera
        {
            Position = new Point3D(0, -21, 17),
            LookDirection = new Vector3D(0, 21, -11),
            UpDirection = new Vector3D(0, 0, 1),
            FieldOfView = 48
        };
        Tactical3DViewport.Camera = camera;

        var scene = new Model3DGroup();
        scene.Children.Add(new AmbientLight(Color.FromRgb(80, 92, 100)));
        scene.Children.Add(new DirectionalLight(Color.FromRgb(225, 230, 224), new Vector3D(-1, 1, -2)));

        var elevations = new List<double>();
        for (var y = 0; y < _state.Height; y++)
        for (var x = 0; x < _state.Width; x++)
            elevations.Add(_state.World.GroundAltitude(new GridPoint(x, y)));
        var minimumElevation = elevations.Count == 0 ? 0 : elevations.Min();
        const double verticalScale = 0.035;
        var halfWidth = _state.Width / 2.0;
        var halfHeight = _state.Height / 2.0;

        for (var y = 0; y < _state.Height; y++)
        for (var x = 0; x < _state.Width; x++)
        {
            var point = new GridPoint(x, y);
            var altitude = _state.World.GroundAltitude(point);
            var top = 0.12 + (altitude - minimumElevation) * verticalScale;
            var centerX = x + 0.5 - halfWidth;
            var centerY = (_state.Height - y - 0.5) - halfHeight;
            var color = TileColor(_state.Tiles[x, y], altitude);
            scene.Children.Add(CreateBoxModel(
                new Point3D(centerX, centerY, top / 2.0),
                0.96, 0.96, Math.Max(0.12, top), color));
        }

        foreach (var unit in _state.Units.Where(unit => unit.Alive))
        {
            if (FogToggle.IsChecked == true && !UnitVisibleToPerspective(unit)) continue;
            var altitude = _state.World.GroundAltitude(unit.Position);
            var z = 0.12 + (altitude - minimumElevation) * verticalScale + 0.34;
            var x = unit.Position.X + 0.5 - halfWidth;
            var y = (_state.Height - unit.Position.Y - 0.5) - halfHeight;
            var selected = _state.Units.IndexOf(unit) == _selectedUnitIndex;
            var size = selected ? 0.48 :
                (unit.UnitClass is TacticalUnitClass.Vehicle or TacticalUnitClass.ArmoredVehicle or TacticalUnitClass.Tank or TacticalUnitClass.Apc ? 0.42 : 0.28);
            var color = unit.Faction == TacticalFaction.Blue
                ? Color.FromRgb(74, 156, 230)
                : Color.FromRgb(220, 82, 72);
            scene.Children.Add(CreateBoxModel(new Point3D(x, y, z), size, size, selected ? 0.72 : 0.52, color));
        }

        Tactical3DViewport.Children.Add(new ModelVisual3D { Content = scene });
    }

    private static GeometryModel3D CreateBoxModel(Point3D center, double width, double depth, double height, Color color)
    {
        var hx = width / 2.0;
        var hy = depth / 2.0;
        var hz = height / 2.0;
        var mesh = new MeshGeometry3D
        {
            Positions = new Point3DCollection
            {
                new(center.X - hx, center.Y - hy, center.Z - hz),
                new(center.X + hx, center.Y - hy, center.Z - hz),
                new(center.X + hx, center.Y + hy, center.Z - hz),
                new(center.X - hx, center.Y + hy, center.Z - hz),
                new(center.X - hx, center.Y - hy, center.Z + hz),
                new(center.X + hx, center.Y - hy, center.Z + hz),
                new(center.X + hx, center.Y + hy, center.Z + hz),
                new(center.X - hx, center.Y + hy, center.Z + hz)
            },
            TriangleIndices = new Int32Collection
            {
                0,2,1, 0,3,2,
                4,5,6, 4,6,7,
                0,1,5, 0,5,4,
                1,2,6, 1,6,5,
                2,3,7, 2,7,6,
                3,0,4, 3,4,7
            }
        };
        var material = new DiffuseMaterial(new SolidColorBrush(color));
        return new GeometryModel3D(mesh, material) { BackMaterial = material };
    }

    private void RenderMap()
    {
        MapCanvas.Width = _state.Width * CellSize;
        MapCanvas.Height = _state.Height * CellSize;
        MapCanvas.Children.Clear();
        DrawTerrain();
        DrawObjectives();
        if (PathToggle.IsChecked == true) DrawOrders();
        if (GridToggle.IsChecked == true) DrawGridLabels();
        if (_selectedGridCell is not null && _selectedKeypad is not null) DrawSelectedGridReference();
        DrawRecentCombat();
        DrawSelectedLos();
        DrawUnits();
    }

    private void DrawObjectives()
    {
        foreach (var objective in _state.Objectives)
        {
            var center = Center(objective.Position);
            var diameter = CellSize * (objective.CaptureRadiusCells * 2 + 0.72);
            var zone = new Ellipse
            {
                Width = diameter,
                Height = diameter,
                Fill = new SolidColorBrush(Color.FromArgb(38, 241, 199, 91)),
                Stroke = new SolidColorBrush(Color.FromArgb(220, 241, 199, 91)),
                StrokeThickness = 2.2,
                StrokeDashArray = [5, 3],
                IsHitTestVisible = false
            };
            Canvas.SetLeft(zone, center.X - diameter / 2);
            Canvas.SetTop(zone, center.Y - diameter / 2);
            MapCanvas.Children.Add(zone);

            var label = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(210, 50, 42, 19)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(241, 199, 91)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),
                Padding = new Thickness(5, 2, 5, 2),
                Child = new TextBlock
                {
                    Text = $"OBJECTIVE {VillageGridReference.ShortCellLabel(objective.Position)}",
                    Foreground = new SolidColorBrush(Color.FromRgb(255, 225, 145)),
                    FontSize = 9,
                    FontWeight = FontWeights.Bold
                },
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, center.X - 48);
            Canvas.SetTop(label, center.Y - 12);
            MapCanvas.Children.Add(label);
        }
    }

    private void DrawRecentCombat()
    {
        foreach (var item in _state.CombatEvents
                     .Where(item => item.Tick >= _state.Tick - 2)
                     .TakeLast(16))
        {
            var source = item.SourceUnitId is null ? null : _state.Units.FirstOrDefault(unit => unit.Id == item.SourceUnitId);
            var target = item.TargetUnitId is null ? null : _state.Units.FirstOrDefault(unit => unit.Id == item.TargetUnitId);
            if (item.Type == CombatEventType.Fire && source is not null && target is not null)
            {
                var from = VisualCenter(source);
                var to = VisualCenter(target);
                MapCanvas.Children.Add(new Line
                {
                    X1 = from.X,
                    Y1 = from.Y,
                    X2 = to.X,
                    Y2 = to.Y,
                    Stroke = new SolidColorBrush(Color.FromArgb(230, 255, 190, 72)),
                    StrokeThickness = 2.4,
                    StrokeDashArray = [8, 5],
                    IsHitTestVisible = false
                });
            }
            if (item.Type == CombatEventType.Hit && target is not null)
            {
                var point = VisualCenter(target);
                var flash = new Ellipse
                {
                    Width = 34,
                    Height = 34,
                    Stroke = new SolidColorBrush(Color.FromArgb(235, 255, 67, 55)),
                    StrokeThickness = 3,
                    Fill = Brushes.Transparent,
                    IsHitTestVisible = false
                };
                Canvas.SetLeft(flash, point.X - 17);
                Canvas.SetTop(flash, point.Y - 17);
                MapCanvas.Children.Add(flash);
            }
        }
    }

    private void DrawTerrain()
    {
        for (var y = 0; y < _state.Height; y++)
        for (var x = 0; x < _state.Width; x++)
        {
            var point = new GridPoint(x, y);
            var context = _state.World.Context(point);
            var rectangle = new Rectangle
            {
                Width = CellSize,
                Height = CellSize,
                Fill = new SolidColorBrush(TileColor(_state.Tiles[x, y], context.GroundAltitudeMeters)),
                Stroke = GridToggle.IsChecked == true
                    ? new SolidColorBrush(Color.FromArgb(82, 220, 231, 236))
                    : Brushes.Transparent,
                StrokeThickness = 0.75,
                IsHitTestVisible = false,
                ToolTip = $"{VillageGridReference.CellLabel(point)} • Z {context.GroundAltitudeMeters:F0} m • {context.Area}"
            };
            Canvas.SetLeft(rectangle, x * CellSize);
            Canvas.SetTop(rectangle, y * CellSize);
            MapCanvas.Children.Add(rectangle);
        }
    }

    private void DrawGridLabels()
    {
        for (var y = 0; y < _state.Height; y++)
        for (var x = 0; x < _state.Width; x++)
        {
            var label = new TextBlock
            {
                Text = VillageGridReference.ShortCellLabel(new GridPoint(x, y)),
                Foreground = new SolidColorBrush(Color.FromArgb(205, 238, 244, 247)),
                FontSize = 8,
                FontFamily = new FontFamily("Consolas"),
                FontWeight = FontWeights.SemiBold,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, x * CellSize + 3);
            Canvas.SetTop(label, y * CellSize + 2);
            MapCanvas.Children.Add(label);
        }
    }

    private void DrawSelectedGridReference()
    {
        var cell = _selectedGridCell!.Value;
        var keypad = _selectedKeypad!.Value;
        var left = cell.X * CellSize;
        var top = cell.Y * CellSize;

        var outline = new Rectangle
        {
            Width = CellSize,
            Height = CellSize,
            Fill = new SolidColorBrush(Color.FromArgb(28, 72, 184, 160)),
            Stroke = new SolidColorBrush(Color.FromRgb(89, 225, 194)),
            StrokeThickness = 2.4,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(outline, left);
        Canvas.SetTop(outline, top);
        MapCanvas.Children.Add(outline);

        for (var index = 1; index <= 2; index++)
        {
            MapCanvas.Children.Add(new Line
            {
                X1 = left + CellSize * index / 3.0,
                Y1 = top,
                X2 = left + CellSize * index / 3.0,
                Y2 = top + CellSize,
                Stroke = new SolidColorBrush(Color.FromArgb(190, 235, 245, 248)),
                StrokeThickness = 0.8,
                IsHitTestVisible = false
            });
            MapCanvas.Children.Add(new Line
            {
                X1 = left,
                Y1 = top + CellSize * index / 3.0,
                X2 = left + CellSize,
                Y2 = top + CellSize * index / 3.0,
                Stroke = new SolidColorBrush(Color.FromArgb(190, 235, 245, 248)),
                StrokeThickness = 0.8,
                IsHitTestVisible = false
            });
        }

        var keypadColumn = (keypad - 1) % 3;
        var keypadRowFromBottom = (keypad - 1) / 3;
        var keypadRowFromTop = 2 - keypadRowFromBottom;
        var third = CellSize / 3.0;
        var highlight = new Rectangle
        {
            Width = third,
            Height = third,
            Fill = new SolidColorBrush(Color.FromArgb(105, 72, 184, 160)),
            Stroke = new SolidColorBrush(Color.FromRgb(220, 255, 246)),
            StrokeThickness = 1.2,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(highlight, left + keypadColumn * third);
        Canvas.SetTop(highlight, top + keypadRowFromTop * third);
        MapCanvas.Children.Add(highlight);
    }

    private void DrawSelectedLos()
    {
        var selected = SelectedUnit();
        var opponent = selected is null ? null : NearestOpponent(selected);
        if (selected is null || opponent is null) return;
        var los = LineOfSightEngine.Evaluate(_state, selected, opponent);
        var color = los.State switch
        {
            LineOfSightState.Clear => Color.FromArgb(175, 87, 221, 169),
            LineOfSightState.Obscured => Color.FromArgb(190, 238, 191, 77),
            _ => Color.FromArgb(190, 229, 92, 83)
        };
        MapCanvas.Children.Add(new Line
        {
            X1 = VisualCenter(selected).X,
            Y1 = VisualCenter(selected).Y,
            X2 = VisualCenter(opponent).X,
            Y2 = VisualCenter(opponent).Y,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            StrokeDashArray = [5, 3],
            IsHitTestVisible = false
        });
    }

    private void DrawOrders()
    {
        foreach (var unit in _state.Units.Where(unit => unit.Alive && unit.Path.Count > 0 && UnitVisibleToPerspective(unit)))
        {
            var points = new PointCollection { VisualCenter(unit) };
            foreach (var point in unit.Path) points.Add(Center(point));
            var line = new Polyline
            {
                Points = points,
                Stroke = unit.Faction == TacticalFaction.Blue
                    ? new SolidColorBrush(Color.FromArgb(185, 92, 179, 255))
                    : new SolidColorBrush(Color.FromArgb(185, 255, 111, 97)),
                StrokeThickness = 3,
                StrokeDashArray = [3, 2],
                IsHitTestVisible = false
            };
            MapCanvas.Children.Add(line);
        }
    }

    private void DrawUnits()
    {
        for (var index = 0; index < _state.Units.Count; index++)
        {
            var unit = _state.Units[index];
            if (!UnitVisibleToPerspective(unit)) continue;

            var selected = index == _selectedUnitIndex;
            var center = VisualCenter(unit);
            if (!unit.Alive)
            {
                var casualtyColor = new SolidColorBrush(Color.FromArgb(220, 180, 184, 188));
                MapCanvas.Children.Add(new Line { X1 = center.X - 9, Y1 = center.Y - 9, X2 = center.X + 9,
                    Y2 = center.Y + 9, Stroke = casualtyColor, StrokeThickness = 3, IsHitTestVisible = false });
                MapCanvas.Children.Add(new Line { X1 = center.X + 9, Y1 = center.Y - 9, X2 = center.X - 9,
                    Y2 = center.Y + 9, Stroke = casualtyColor, StrokeThickness = 3, IsHitTestVisible = false });
                var deadLabel = new TextBlock { Text = ShortUnitName(unit) + " • KIA", Foreground = casualtyColor,
                    FontSize = 8, Background = new SolidColorBrush(Color.FromArgb(155, 8, 15, 21)), IsHitTestVisible = false };
                Canvas.SetLeft(deadLabel, center.X + 12);
                Canvas.SetTop(deadLabel, center.Y - 8);
                MapCanvas.Children.Add(deadLabel);
                continue;
            }

            Shape marker = unit.UnitClass is TacticalUnitClass.Vehicle or TacticalUnitClass.ArmoredVehicle or TacticalUnitClass.Tank or TacticalUnitClass.Apc
                ? new Rectangle { RadiusX = 3, RadiusY = 3 }
                : new Ellipse();
            marker.Width = selected ? 30 : 26;
            marker.Height = selected ? 27 : 23;
            marker.Fill = new SolidColorBrush(FactionColor(unit.Faction));
            var incapacitated = unit.Soldier is { IsCombatEffective: false };
            marker.Stroke = selected ? Brushes.White : incapacitated
                ? new SolidColorBrush(Color.FromRgb(255, 205, 74))
                : new SolidColorBrush(Color.FromRgb(18, 29, 38));
            marker.StrokeThickness = selected ? 3 : 2;
            var command = _state.CommandFor(unit);
            marker.ToolTip = $"{unit.DisplayName} • {unit.UnitClass} • {VillageGridReference.CellLabel(unit.Position)} • {command?.StatusText ?? "no order"}";
            Canvas.SetLeft(marker, center.X - marker.Width / 2);
            Canvas.SetTop(marker, center.Y - marker.Height / 2);
            MapCanvas.Children.Add(marker);

            var roleLabel = new TextBlock
            {
                Text = UnitRoleCode(unit),
                Foreground = Brushes.White,
                FontSize = unit.Soldier?.Role == SoldierRole.AutomaticRifleman ? 7 : 8,
                FontWeight = FontWeights.Bold,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(roleLabel, center.X - (UnitRoleCode(unit).Length * 2.4));
            Canvas.SetTop(roleLabel, center.Y - 6);
            MapCanvas.Children.Add(roleLabel);

            if (unit.Soldier is not null)
            {
                var health = Math.Clamp(unit.Soldier.Vitals.HitPoints / 100.0, 0, 1);
                var barBack = new Rectangle { Width = 28, Height = 4, Fill = new SolidColorBrush(Color.FromArgb(210, 25, 31, 36)),
                    Stroke = Brushes.Black, StrokeThickness = 0.5, IsHitTestVisible = false };
                var bar = new Rectangle { Width = 28 * health, Height = 4,
                    Fill = new SolidColorBrush(health > 0.6 ? Color.FromRgb(66, 207, 128) :
                        health > 0.3 ? Color.FromRgb(241, 190, 65) : Color.FromRgb(238, 79, 67)), IsHitTestVisible = false };
                Canvas.SetLeft(barBack, center.X - 14); Canvas.SetTop(barBack, center.Y + 15);
                Canvas.SetLeft(bar, center.X - 14); Canvas.SetTop(bar, center.Y + 15);
                MapCanvas.Children.Add(barBack); MapCanvas.Children.Add(bar);
            }

            var actionText = command?.Action.ToString().ToUpperInvariant() ?? "HOLDING";
            var label = new TextBlock
            {
                Text = $"{ShortUnitName(unit)} • {actionText}",
                Foreground = new SolidColorBrush(Color.FromRgb(238, 244, 247)),
                Background = new SolidColorBrush(Color.FromArgb(165, 8, 15, 21)),
                FontSize = 8,
                FontWeight = FontWeights.SemiBold,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(label, center.X + 17);
            Canvas.SetTop(label, center.Y - 12);
            MapCanvas.Children.Add(label);
        }
    }

    private bool UnitVisibleToPerspective(TacticalUnit unit)
    {
        if (FogToggle.IsChecked != true) return true;
        var perspective = PerspectiveFaction();
        if (unit.Faction == perspective) return true;
        return _state.Knowledge[perspective].Knows(unit.Id, _state.Tick);
    }

    private void UpdateLocationPanel()
    {
        SectorText.Text = _activeVillage.Sector;
        VillageText.Text = _activeVillage.Name;
        MapTitleText.Text = $"{_activeVillage.Sector} • {_activeVillage.Name} • 13×13 • local XYZ";
        if (_selectedGridCell is null || _selectedKeypad is null)
        {
            GridReferenceText.Text = "—";
            KeypadRefText.Text = "—";
        }
    }

    private void UpdateInspector()
    {
        TickText.Text = $"{_state.Tick / 60:00}:{_state.Tick % 60:00}";
        var unit = SelectedUnit();
        if (unit is null)
        {
            UnitIdText.Text = "—";
            FactionText.Text = "—";
            PositionText.Text = "—";
            WorldPositionText.Text = "—";
            AltitudeText.Text = "—";
            TerrainText.Text = "—";
            TerritoryText.Text = "—";
            RouteText.Text = "—";
            OpponentText.Text = "—";
            LosText.Text = "—";
            DetectionText.Text = "—";
            ReciprocalText.Text = "—";
            UnitStatusText.Text = "Select a visible entity to inspect its local XYZ, environment, LOS and knowledge state.";
            return;
        }

        var context = _state.ContextOf(unit);
        var world = _state.PositionOf(unit);
        var territory = _state.TerritoryFor(unit.Faction, unit.Position);
        var opponent = NearestOpponent(unit);

        UnitIdText.Text = $"{unit.DisplayName}  #{unit.Id}";
        FactionText.Text = $"{unit.Faction} / {unit.UnitClass}";
        FactionText.Foreground = new SolidColorBrush(FactionColor(unit.Faction));
        PositionText.Text = VillageGridReference.CellLabel(unit.Position);
        WorldPositionText.Text = $"{world.X:F0} / {world.Y:F0} / {world.Z:F1} m";
        AltitudeText.Text = $"{context.GroundAltitudeMeters:F1} m local datum";
        TerrainText.Text = $"{context.Terrain} / {context.Area}";
        TerritoryText.Text = territory.ToString();
        var command = _state.CommandFor(unit);
        RouteText.Text = command is null
            ? unit.Path.Count == 0 ? "No order / Holding" : $"{unit.Path.Count} cells / Moving"
            : $"{command.Order} → {VillageGridReference.ShortCellLabel(command.Objective)} • {command.Action}";

        if (opponent is null)
        {
            OpponentText.Text = "None";
            LosText.Text = "—";
            DetectionText.Text = "—";
            ReciprocalText.Text = "—";
        }
        else
        {
            var los = LineOfSightEngine.Evaluate(_state, unit, opponent);
            OpponentText.Text = $"{opponent.DisplayName} #{opponent.Id}";
            LosText.Text = $"{los.State} • {los.HorizontalDistanceMeters:F0} m • obsc {los.ObscurationFactor:P0}";

            var perspective = PerspectiveFaction();
            var perspectiveTarget = unit.Faction == perspective ? opponent : unit;
            DetectionText.Text = FormatKnowledge(perspective, perspectiveTarget);
            ReciprocalText.Text = FormatKnowledge(opponent.Faction, unit);
        }

        if (unit.Soldier is null)
        {
            UnitStatusText.Text = "LOS is geometric. Detection is separate, probabilistic and faction-specific; the opposite side's knowledge is not automatically revealed in gameplay logic.";
        }
        else
        {
            var soldier = unit.Soldier;
            var weapon = soldier.PrimaryWeapon;
            var wounds = soldier.Vitals.Wounds.Count == 0
                ? "none"
                : string.Join(", ", soldier.Vitals.Wounds.Select(wound =>
                    $"{wound.Region}:{wound.Type}:{wound.Severity01:P0}{(wound.Treated ? ":treated" : "")}"));
            UnitStatusText.Text =
                $"{soldier.Role} | HP {soldier.Vitals.HitPoints:F0}/100 | blood {soldier.Vitals.BloodVolume01:P0} | " +
                $"{soldier.Vitals.Condition} | suppression {soldier.Vitals.Suppression01:P0} | fatigue {soldier.Vitals.Fatigue01:P0}\n" +
                $"{weapon.Definition.DisplayName} ({weapon.Definition.Class}) | {weapon.RoundsLoaded}/{weapon.ReserveRounds} rds | " +
                $"cyclic {weapon.Definition.CyclicRateRpm:F0} rpm / sustained {weapon.Definition.SustainedRateRpm:F0} rpm | " +
                $"practical range {weapon.Definition.PracticalEngagementRangeMeters:F0} m | optic {weapon.Optic.Name} | load {soldier.CarriedMassKg:F1} kg\n" +
                $"Wounds: {wounds}. Environment: {_state.Environment.Light}/{_state.Environment.Visibility}; weapon physical range is not reduced at night — target acquisition is.";
        }
    }

    private TacticalUnit? SelectedUnit() =>
        _selectedUnitIndex >= 0 && _selectedUnitIndex < _state.Units.Count
            ? _state.Units[_selectedUnitIndex]
            : null;

    private TacticalUnit? NearestOpponent(TacticalUnit unit) =>
        _state.Units
            .Where(candidate => candidate.Alive && candidate.Faction != unit.Faction)
            .OrderBy(candidate => _state.PositionOf(unit).HorizontalDistanceTo(_state.PositionOf(candidate)))
            .FirstOrDefault();

    private string FormatKnowledge(TacticalFaction faction, TacticalUnit target)
    {
        var knowledge = _state.Knowledge[faction];
        if (!knowledge.Knows(target.Id, _state.Tick)) return $"{faction}: no current contact";
        var contact = knowledge.GetContact(target.Id)!;
        return $"{faction}: {contact.Classification} • det {contact.DetectionConfidence:P0} • id {contact.IdentificationConfidence:P0}";
    }

    private void UpdateSimulationControls()
    {
        StartPauseButton.Content = _state.Result != BattleResult.Ongoing ? "✓  Complete" :
            _running ? "Ⅱ  Pause" : "▶  Start";
        ModeText.Text = _state.Result != BattleResult.Ongoing ? "COMPLETE" : _running ? "RUNNING" : "PAUSED";
        ModeText.Foreground = _state.Result != BattleResult.Ongoing
            ? new SolidColorBrush(Color.FromRgb(241, 199, 91))
            : _running
            ? new SolidColorBrush(Color.FromRgb(85, 205, 164))
            : new SolidColorBrush(Color.FromRgb(241, 199, 91));
    }

    private void UpdateBattleHud()
    {
        BattleTitleText.Text = _state.ScenarioName.ToUpperInvariant();
        BattlePhaseText.Text = $"{_state.Phase.ToString().ToUpperInvariant()} • T+{_state.Tick / 60:00}:{_state.Tick % 60:00}";
        MissionNameText.Text = _state.ScenarioName;
        MissionBriefingText.Text = _state.MissionBriefing;
        MissionPhaseText.Text = _state.Result == BattleResult.Ongoing
            ? $"{_state.Phase} — autonomous simulation"
            : $"Complete — {_state.Result}";

        var blueTotal = _state.Units.Count(unit => unit.Faction == TacticalFaction.Blue && unit.Soldier is not null);
        var redTotal = _state.Units.Count(unit => unit.Faction == TacticalFaction.Red && unit.Soldier is not null);
        var blueEffective = _state.Units.Count(unit => unit.Faction == TacticalFaction.Blue && unit.Alive &&
            unit.Soldier is { IsCombatEffective: true });
        var redEffective = _state.Units.Count(unit => unit.Faction == TacticalFaction.Red && unit.Alive &&
            unit.Soldier is { IsCombatEffective: true });
        var blueWounded = _state.Units.Count(unit => unit.Faction == TacticalFaction.Blue && unit.Alive &&
            unit.Soldier?.Vitals.Wounds.Count > 0);
        var redWounded = _state.Units.Count(unit => unit.Faction == TacticalFaction.Red && unit.Alive &&
            unit.Soldier?.Vitals.Wounds.Count > 0);
        BlueStrengthText.Text = $"{blueEffective} effective / {blueTotal} • {blueWounded} WIA";
        RedStrengthText.Text = $"{redEffective} effective / {redTotal} • {redWounded} WIA";

        var objective = _state.Objectives.FirstOrDefault();
        var objectiveState = "no objective";
        if (objective is not null)
        {
            var blueNear = _state.Units.Any(unit => unit.Faction == TacticalFaction.Blue && unit.Alive &&
                unit.Soldier is { IsCombatEffective: true } && Manhattan(unit.Position, objective.Position) <= objective.CaptureRadiusCells);
            var redNear = _state.Units.Any(unit => unit.Faction == TacticalFaction.Red && unit.Alive &&
                unit.Soldier is { IsCombatEffective: true } && Manhattan(unit.Position, objective.Position) <= objective.CaptureRadiusCells);
            objectiveState = blueNear && redNear ? "CONTESTED" :
                blueNear ? $"BLUE {objective.BlueControlSeconds}/{objective.RequiredControlSeconds}s" :
                redNear ? $"RED {objective.RedControlSeconds}/{objective.RequiredControlSeconds}s" : "NEUTRAL";
            ObjectiveControlText.Text = $"{objective.DisplayName} — {objectiveState}";
        }
        BattleScoreText.Text = $"BLUE {blueEffective}/{blueTotal}  •  RED {redEffective}/{redTotal}  •  G07 {objectiveState}";
        BattleResultText.Text = _state.Result == BattleResult.Ongoing ? "Result: ongoing" : $"Result: {_state.Result}";
        BattleResultText.Foreground = new SolidColorBrush(_state.Result switch
        {
            BattleResult.BlueVictory => FactionColor(TacticalFaction.Blue),
            BattleResult.RedVictory => FactionColor(TacticalFaction.Red),
            BattleResult.Draw => Color.FromRgb(241, 199, 91),
            _ => Color.FromRgb(184, 199, 208)
        });
    }

    private void UpdateRecentAction()
    {
        var activity = _state.ActivityEvents.Select(item =>
            (item.Tick, Text: $"[{item.Type.ToString().ToUpperInvariant()}] {item.Message}"));
        var combat = _state.CombatEvents.Select(item =>
            (item.Tick, Text: $"[{item.Type.ToString().ToUpperInvariant()}] {item.Message}"));
        var recent = activity.Concat(combat)
            .OrderByDescending(item => item.Tick)
            .Take(4)
            .Select(item => $"T+{item.Tick:000}  {item.Text}")
            .ToArray();
        RecentActionText.Text = recent.Length == 0
            ? "Orders loaded. Press Start to begin."
            : string.Join(Environment.NewLine, recent);
    }

    private void OrbatDepthBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_initialized) PopulateOrbatTrees();
    }

    private string CurrentOrbatDepth()
    {
        return OrbatDepthBox?.SelectedItem is ComboBoxItem item && item.Tag is string value
            ? value
            : "Tactical";
    }

    private void PopulateOrbatTrees()
    {
        BlueOrbatTree.Items.Clear();
        RedOrbatTree.Items.Clear();
        if (_scenarioOrbat is null) return;

        OrbatScenarioText.Text = $"{_scenarioOrbat.ScenarioName} • {CurrentOrbatDepth()} depth";
        OrbatBoundaryText.Text = _scenarioOrbat.DataBoundary;
        var blueRoot = _scenarioOrbat.Find(_scenarioOrbat.BlueRootId);
        var redRoot = _scenarioOrbat.Find(_scenarioOrbat.RedRootId);
        if (blueRoot is not null) BlueOrbatTree.Items.Add(BuildOrbatNode(blueRoot));
        if (redRoot is not null) RedOrbatTree.Items.Add(BuildOrbatNode(redRoot));
    }

    private TreeViewItem BuildOrbatNode(OrbatNodeRecord node)
    {
        var item = new TreeViewItem
        {
            Header = CreateOrbatHeader(node),
            Tag = node,
            IsExpanded = node.Echelon is OrbatEchelon.Side or OrbatEchelon.Command or OrbatEchelon.Brigade
        };
        if (_scenarioOrbat is null) return item;
        foreach (var child in _scenarioOrbat.ChildrenOf(node.Id)
                     .Where(ShouldShowOrbatNode)
                     .OrderBy(child => child.Echelon)
                     .ThenBy(child => child.Name, StringComparer.Ordinal))
        {
            item.Items.Add(BuildOrbatNode(child));
        }
        return item;
    }

    private bool ShouldShowOrbatNode(OrbatNodeRecord node)
    {
        var depth = CurrentOrbatDepth();
        if (depth == "Detail") return true;
        if (depth == "Strategic")
            return node.Echelon is OrbatEchelon.Side or OrbatEchelon.Command or OrbatEchelon.Division or
                OrbatEchelon.Brigade or OrbatEchelon.Battalion or OrbatEchelon.SupportElement;

        // Tactical view: Blue expands to platoon; irregular Red expands to its squad/team/cell equivalents.
        if (node.Affiliation == OrbatAffiliation.Blue)
            return node.Echelon is not (OrbatEchelon.Squad or OrbatEchelon.Team or OrbatEchelon.Cell);
        return true;
    }

    private FrameworkElement CreateOrbatHeader(OrbatNodeRecord node)
    {
        var panel = new StackPanel { Orientation = Orientation.Horizontal };
        var color = node.Affiliation == OrbatAffiliation.Blue
            ? Color.FromRgb(42, 98, 150)
            : Color.FromRgb(139, 58, 58);
        var symbol = new Border
        {
            Width = 52,
            Height = 24,
            BorderBrush = new SolidColorBrush(Color.FromRgb(220, 232, 238)),
            BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(color),
            CornerRadius = new CornerRadius(2),
            Margin = new Thickness(0, 1, 7, 1)
        };
        symbol.Child = new TextBlock
        {
            Text = node.RoleLabel,
            Foreground = Brushes.White,
            FontFamily = new FontFamily("Consolas"),
            FontWeight = FontWeights.Bold,
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        panel.Children.Add(symbol);
        panel.Children.Add(new TextBlock
        {
            Text = $"{node.DisplayName}  [{node.EchelonLabel}]",
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = new SolidColorBrush(Color.FromRgb(221, 232, 238)),
            FontSize = 11
        });
        return panel;
    }

    private void OrbatTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is not TreeViewItem item || item.Tag is not OrbatNodeRecord node) return;
        RenderOrbatNode(node);
    }

    private void RenderOrbatNode(OrbatNodeRecord node)
    {
        _selectedOrbatNodeId = node.Id;
        OrbatNodeNameText.Text = node.DisplayName;
        OrbatNodeMetaText.Text = $"{node.Affiliation} • {node.EchelonLabel} • {node.Role} • {node.Status}";
        var strength = node.CurrentPersonnel is null
            ? "personnel n/a"
            : node.AuthorizedPersonnel is null
                ? $"personnel {node.CurrentPersonnel}"
                : $"personnel {node.CurrentPersonnel}/{node.AuthorizedPersonnel}";
        OrbatNodeStateText.Text = $"{strength} • readiness {node.ReadinessPercent}% • morale {node.MoralePercent}% • ammo {node.AmmoPercent}% • comms {node.Communications}";
        OrbatNodeOrderText.Text = $"Order: {node.CurrentOrder}";
        OrbatBoundaryText.Text = _scenarioOrbat?.DataBoundary ?? "Synthetic scenario task organization.";

        var linked = LinkedTacticalUnits(node).ToArray();
        if (linked.Length == 0)
        {
            OrbatLiveLinkText.Text = "Live link: no tactical entities are currently assigned to this branch in the prototype.";
            return;
        }
        var effective = linked.Count(UnitEffective);
        var soldiers = linked.Where(unit => unit.Soldier is not null).Select(unit => unit.Soldier!).ToArray();
        var wounded = soldiers.Count(soldier => soldier.Vitals.Condition.HasFlag(SoldierCondition.Wounded));
        var suppressed = soldiers.Count(soldier => soldier.Vitals.Condition.HasFlag(SoldierCondition.Suppressed));
        var averageHp = soldiers.Length == 0 ? 0 : soldiers.Average(soldier => soldier.Vitals.HitPoints);
        var remainingRounds = soldiers.Sum(soldier => soldier.PrimaryWeapon.TotalRounds);
        OrbatLiveLinkText.Text = $"Live link: {effective}/{linked.Length} effective • {wounded} wounded • {suppressed} suppressed" +
            (soldiers.Length == 0 ? "" : $" • avg HP {averageHp:F0} • small-arms rounds {remainingRounds}");
    }

    private void OrbatHold_Click(object sender, RoutedEventArgs e) =>
        IssueOrbatOrder(TacticalOrderType.Hold, "HOLD", destinationRequired: false);

    private void OrbatAdvance_Click(object sender, RoutedEventArgs e) =>
        IssueOrbatOrder(TacticalOrderType.Advance, "ADVANCE", destinationRequired: true);

    private void OrbatDefend_Click(object sender, RoutedEventArgs e) =>
        IssueOrbatOrder(TacticalOrderType.DefendObjective, "DEFEND", destinationRequired: true);

    private void OrbatSupport_Click(object sender, RoutedEventArgs e) =>
        IssueOrbatOrder(TacticalOrderType.Support, "SUPPORT", destinationRequired: true);

    private void OrbatWithdraw_Click(object sender, RoutedEventArgs e) =>
        IssueOrbatOrder(TacticalOrderType.Withdraw, "WITHDRAW", destinationRequired: true);

    private void IssueOrbatOrder(TacticalOrderType order, string label, bool destinationRequired)
    {
        if (_scenarioOrbat is null || string.IsNullOrWhiteSpace(_selectedOrbatNodeId))
        {
            StatusText.Text = "Select an ORBAT formation first.";
            return;
        }

        var node = _scenarioOrbat.Find(_selectedOrbatNodeId);
        if (node is null) return;
        var units = LinkedTacticalUnits(node).ToArray();
        if (units.Length == 0)
        {
            StatusText.Text = $"{node.DisplayName} has no live tactical entities linked in this prototype.";
            return;
        }

        if (destinationRequired && _selectedGridCell is null)
        {
            StatusText.Text = $"Select a tactical grid reference, then issue {label}.";
            return;
        }

        foreach (var unit in units)
        {
            var objective = destinationRequired ? _selectedGridCell!.Value : unit.Position;
            TacticalAiEngine.AssignOrder(_state, unit, order, objective);
            unit.Path.Clear();
        }

        var destinationText = destinationRequired
            ? $" {VillageGridReference.CellLabel(_selectedGridCell!.Value)}"
            : string.Empty;
        node.CurrentOrder = label + destinationText;
        node.Status = order is TacticalOrderType.Advance or TacticalOrderType.Withdraw
            ? OrbatReadiness.Moving
            : OrbatReadiness.Ready;
        _state.AddActivity(TacticalEventType.Order,
            $"FORMATION ORDER: {node.DisplayName} -> {label}{destinationText}.");
        AddEvent($"[ORBAT] {node.DisplayName} -> {label}{destinationText} ({units.Length} linked entities)");
        RenderOrbatNode(node);
        RenderAll();
        StatusText.Text = $"{label} order issued to {node.DisplayName}.";
    }

    private IEnumerable<TacticalUnit> LinkedTacticalUnits(OrbatNodeRecord node)
    {
        if (_scenarioOrbat is null) yield break;
        var branchIds = _scenarioOrbat.DescendantsAndSelf(node.Id).Select(item => item.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var unit in _state.Units)
        {
            if (_orbatBindings.TryGetValue(unit.Id, out var nodeId) && branchIds.Contains(nodeId))
                yield return unit;
        }
    }

    private static bool UnitEffective(TacticalUnit unit) => unit.Alive && (unit.Soldier?.IsCombatEffective ?? true);

    private void BindTacticalUnitsToOrbat()
    {
        _orbatBindings.Clear();
        if (_scenarioOrbat is null) return;
        var blueInf = _scenarioOrbat.Nodes.Where(node => node.Affiliation == OrbatAffiliation.Blue && node.Echelon == OrbatEchelon.Platoon && (node.Role is OrbatRole.Infantry or OrbatRole.Mechanized)).ToArray();
        var blueArmor = _scenarioOrbat.Nodes.Where(node => node.Affiliation == OrbatAffiliation.Blue && node.Echelon == OrbatEchelon.Platoon && node.Role == OrbatRole.Armored).ToArray();
        var redMixed = _scenarioOrbat.Nodes.Where(node => node.Affiliation == OrbatAffiliation.Red && (node.Echelon is OrbatEchelon.Squad or OrbatEchelon.Team) && (node.Role is OrbatRole.Mixed or OrbatRole.Infantry)).ToArray();
        var redAt = _scenarioOrbat.Nodes.Where(node => node.Affiliation == OrbatAffiliation.Red && node.Role == OrbatRole.AntiArmor).ToArray();
        var redRecon = _scenarioOrbat.Nodes.Where(node => node.Affiliation == OrbatAffiliation.Red && (node.Role is OrbatRole.Reconnaissance or OrbatRole.SniperMarksman)).ToArray();
        var blueInfIndex = 0;
        var blueArmorIndex = 0;
        var redMixedIndex = 0;
        var redAtIndex = 0;
        var redReconIndex = 0;

        foreach (var unit in _state.Units)
        {
            OrbatNodeRecord? target = null;
            if (unit.Faction == TacticalFaction.Blue)
            {
                var pool = unit.UnitClass is TacticalUnitClass.ArmoredVehicle or TacticalUnitClass.Tank or TacticalUnitClass.Apc
                    ? blueArmor
                    : blueInf;
                if (pool.Length > 0)
                {
                    var index = unit.UnitClass is TacticalUnitClass.ArmoredVehicle or TacticalUnitClass.Tank or TacticalUnitClass.Apc
                        ? blueArmorIndex++
                        : blueInfIndex++;
                    target = pool[index % pool.Length];
                }
            }
            else if (unit.Faction == TacticalFaction.Red)
            {
                var role = unit.Soldier?.Role;
                if (role == SoldierRole.AntiArmorSpecialist && redAt.Length > 0)
                    target = redAt[redAtIndex++ % redAt.Length];
                else if ((role is SoldierRole.Marksman or SoldierRole.Scout) && redRecon.Length > 0)
                    target = redRecon[redReconIndex++ % redRecon.Length];
                else if (redMixed.Length > 0)
                    target = redMixed[redMixedIndex++ % redMixed.Length];
            }
            if (target is not null) _orbatBindings[unit.Id] = target.Id;
        }
    }

    private void UpdateOrbatLiveSummary()
    {
        if (_scenarioOrbat is null) return;
        var blueUnits = _state.Units.Where(unit => unit.Faction == TacticalFaction.Blue).ToArray();
        var redUnits = _state.Units.Where(unit => unit.Faction == TacticalFaction.Red).ToArray();
        var blueEffective = blueUnits.Count(UnitEffective);
        var redEffective = redUnits.Count(UnitEffective);
        OrbatBattlePhaseText.Text = _state.Result == BattleResult.Ongoing ? _state.Phase.ToString() : _state.Result.ToString();
        OrbatBlueLiveText.Text = $"BLUE {blueEffective}/{blueUnits.Length} effective";
        OrbatRedLiveText.Text = $"RED {redEffective}/{redUnits.Length} effective";
        OrbatContactText.Text = _state.FirstContactTick is null
            ? "No confirmed contact"
            : $"First contact T+{_state.FirstContactTick:000}s • sim T+{_state.Tick:000}s";

        if (!string.IsNullOrWhiteSpace(_selectedOrbatNodeId))
        {
            var selected = _scenarioOrbat.Find(_selectedOrbatNodeId);
            if (selected is not null) RenderOrbatNode(selected);
        }
    }

    private void PopulateFormationTree()
    {
        FormationTree.Items.Clear();
        if (_formationDataset is null) return;
        var root = _formationDataset.Find(_formationDataset.RootId);
        if (root is null) return;
        FormationTree.Items.Add(BuildFormationNode(root));
    }

    private TreeViewItem BuildFormationNode(FormationRecord formation)
    {
        var item = new TreeViewItem
        {
            Header = formation.DisplayName,
            Tag = formation,
            IsExpanded = formation.Level is UnitLevel.ArmedForces or UnitLevel.Headquarters or UnitLevel.RegionalCommand
        };
        if (_formationDataset is not null)
        {
            foreach (var child in _formationDataset.ChildrenOf(formation.Id)
                         .OrderBy(child => child.Level)
                         .ThenBy(child => child.Number ?? int.MaxValue)
                         .ThenBy(child => child.Name, StringComparer.Ordinal))
            {
                item.Items.Add(BuildFormationNode(child));
            }
        }
        return item;
    }

    private void FormationTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (FormationTree.SelectedItem is not TreeViewItem item || item.Tag is not FormationRecord formation ||
            _formationDataset is null)
        {
            return;
        }

        FormationNameText.Text = formation.DisplayName + (string.IsNullOrWhiteSpace(formation.Nickname) ? "" : $" — {formation.Nickname}");
        FormationMetaText.Text = $"{formation.Level} • {formation.Type} • {formation.Status} • public baseline {_formationDataset.AsOf}";
        var children = _formationDataset.ChildrenOf(formation.Id).Count();
        FormationChildrenText.Text = $"Direct children: {children} • descendants: {_formationDataset.DescendantCount(formation.Id)}";
        FormationNotesText.Text = formation.Notes.Count == 0 ? "No additional public-baseline note." : string.Join("  •  ", formation.Notes);
        FormationBoundaryText.Text = _formationDataset.DataBoundary;
    }

    private void VehicleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (VehicleList.SelectedItem is not VehicleDefinition vehicle) return;
        VehicleNameText.Text = vehicle.DisplayName;
        VehicleStatusText.Text = $"{vehicle.Kind} • {vehicle.PublicStatus}";
        VehiclePhysicalText.Text = $"Public nominal: {vehicle.Dimensions.LengthMeters:F2} × {vehicle.Dimensions.WidthMeters:F2} × {vehicle.Dimensions.HeightMeters:F2} m" +
            (vehicle.PublicMassTonnes is null ? "" : $" • {vehicle.PublicMassTonnes:F1} t") +
            $" • crew {vehicle.Crew.Crew}" + (vehicle.Crew.Passengers > 0 ? $" + {vehicle.Crew.Passengers} passengers" : "");
        VehicleMobilityText.Text = "Mobility: " +
            $"{vehicle.Mobility.Type}" +
            FormatNullable(vehicle.Mobility.PublicEnginePowerHp, " • {0} hp") +
            FormatNullable(vehicle.Mobility.PublicMaximumRoadSpeedKph, " • {0:F0} km/h") +
            FormatNullable(vehicle.Mobility.PublicVehicleRangeKm, " • range {0:F0} km") +
            FormatNullable(vehicle.Mobility.PublicFuelCapacityLiters, " • fuel {0:F0} L");
        VehicleWeaponText.Text = vehicle.MainWeapon is null
            ? "Main weapon: not represented in this baseline."
            : $"Main weapon: {vehicle.MainWeapon.Name}" +
              FormatNullable(vehicle.MainWeapon.PublicAmmunitionCapacity, " • public capacity {0}") +
              (vehicle.MainWeapon.GameBaseReloadSeconds is null ? "" : $" • game reload baseline {vehicle.MainWeapon.GameBaseReloadSeconds:F1} s");
        VehicleOpticsText.Text = $"Optics/game model: commander thermal {YesNo(vehicle.Optics.Commander.Thermal)}, gunner thermal {YesNo(vehicle.Optics.Gunner.Thermal)}, panoramic {YesNo(vehicle.Optics.PanoramicAwareness)}, helmet SA {YesNo(vehicle.Optics.HelmetMountedSituationalAwareness)} • SA index {vehicle.Optics.GameSituationalAwareness:F2}";
        VehicleApsText.Text = vehicle.ActiveProtection is null
            ? "APS: none represented."
            : $"APS: {vehicle.ActiveProtection.Name} • installed {YesNo(vehicle.ActiveProtection.Installed)} • {vehicle.ActiveProtection.PublicCoverageDescription} • synthetic game budget {vehicle.ActiveProtection.GameCountermeasureBudget}";
        ArmorZoneList.ItemsSource = vehicle.Protection.Zones.Select(zone =>
            $"{zone.Zone,-18} KE {zone.SyntheticKineticIndex,4}  CE {zone.SyntheticChemicalIndex,4}").ToArray();
        VehicleBoundaryText.Text = vehicle.DataBoundary + " " + vehicle.Protection.ScaleDescription;
    }

    private static string YesNo(bool value) => value ? "yes" : "no";

    private static string FormatNullable<T>(T? value, string format) where T : struct =>
        value is null ? "" : string.Format(CultureInfo.InvariantCulture, format, value.Value);

    private void AddEvent(string message)
    {
        EventList.Items.Insert(0, $"T+{_state.Tick:0000}  {message}");
        while (EventList.Items.Count > 120) EventList.Items.RemoveAt(EventList.Items.Count - 1);
    }

    private void LoadInitialBattleEvents()
    {
        foreach (var activity in _state.ActivityEvents.OrderBy(item => item.Tick))
            AddEvent($"[{activity.Type.ToString().ToUpperInvariant()}] {activity.Message}");
        foreach (var combat in _state.CombatEvents.OrderBy(item => item.Tick))
            AddEvent($"[{combat.Type.ToString().ToUpperInvariant()}] {combat.Message}");
    }

    private static Point Center(GridPoint point) =>
        new(point.X * CellSize + CellSize / 2, point.Y * CellSize + CellSize / 2);

    private Point VisualCenter(TacticalUnit unit)
    {
        var origin = Center(unit.Position);
        if (unit.Path.Count == 0 || unit.MovementProgress <= 0) return origin;
        var destination = unit.Path[0];
        var cost = Math.Max(1, TacticalEngine.MoveCost(_state.Tiles[destination.X, destination.Y]));
        var fraction = Math.Clamp(unit.MovementProgress / (double)cost, 0, 1);
        var target = Center(destination);
        return new Point(origin.X + (target.X - origin.X) * fraction,
            origin.Y + (target.Y - origin.Y) * fraction);
    }

    private static string UnitRoleCode(TacticalUnit unit) => unit.Soldier?.Role switch
    {
        SoldierRole.Leader => "L",
        SoldierRole.Rifleman => "R",
        SoldierRole.AutomaticRifleman => "AR",
        SoldierRole.Marksman => "M",
        SoldierRole.Medic => "+",
        _ => unit.UnitClass is TacticalUnitClass.ArmoredVehicle or TacticalUnitClass.Apc ? "APC" : "U"
    };

    private static string ShortUnitName(TacticalUnit unit) => unit.DisplayName
        .Replace("Blue ", "B-", StringComparison.Ordinal)
        .Replace("Red ", "R-", StringComparison.Ordinal);

    private static int Manhattan(GridPoint first, GridPoint second) =>
        Math.Abs(first.X - second.X) + Math.Abs(first.Y - second.Y);

    private static Color FactionColor(TacticalFaction faction) => faction switch
    {
        TacticalFaction.Blue => Color.FromRgb(77, 163, 255),
        TacticalFaction.Red => Color.FromRgb(238, 88, 77),
        TacticalFaction.Green => Color.FromRgb(83, 190, 125),
        _ => Color.FromRgb(190, 190, 190)
    };

    private static Color TileColor(TacticalTile tile, double altitudeMeters)
    {
        var altitudeShade = (byte)Math.Clamp((int)(altitudeMeters % 60) / 6, 0, 9);
        return tile switch
        {
            TacticalTile.Road => Color.FromRgb((byte)(139 + altitudeShade), (byte)(129 + altitudeShade), (byte)(112 + altitudeShade)),
            TacticalTile.Forest => Color.FromRgb(41, (byte)(73 + altitudeShade), 54),
            TacticalTile.Building => Color.FromRgb((byte)(92 + altitudeShade), (byte)(98 + altitudeShade), (byte)(106 + altitudeShade)),
            TacticalTile.Water => Color.FromRgb(49, 92, 120),
            TacticalTile.Scrub => Color.FromRgb(92, 102, 72),
            TacticalTile.Agricultural => Color.FromRgb(117, 108, 70),
            TacticalTile.Rocky => Color.FromRgb(101, 94, 85),
            _ => Color.FromRgb(82, (byte)(104 + altitudeShade), 84)
        };
    }
}
