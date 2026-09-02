namespace FieldSim.Core;

public enum TacticalTile
{
    Open,
    Road,
    Forest,
    Building,
    Water,
    Scrub,
    Agricultural,
    Rocky
}

public enum TacticalFaction
{
    Blue,
    Red,
    Green,
    Neutral
}

public enum TacticalUnitClass
{
    Person,
    Vehicle,
    ArmoredVehicle,
    Tank,
    Apc,
    AirObject
}

public readonly record struct GridPoint(int X, int Y);

public sealed record MobilityProfile(
    double MaxSpeedMetersPerSecond,
    double AccelerationMetersPerSecondSquared,
    double DecelerationMetersPerSecondSquared)
{
    public static MobilityProfile Infantry => new(1.6, 1.2, 1.8);
    public static MobilityProfile LightVehicle => new(18.0, 2.2, 3.0);
    public static MobilityProfile Apc => new(16.0, 1.8, 2.7);
    public static MobilityProfile ArmoredVehicle => new(14.0, 1.5, 2.4);
    public static MobilityProfile Tank => new(13.0, 1.2, 2.2);
    public static MobilityProfile AirObject => new(55.0, 4.0, 5.0);

    public static MobilityProfile For(TacticalUnitClass unitClass) => unitClass switch
    {
        TacticalUnitClass.Person => Infantry,
        TacticalUnitClass.Vehicle => LightVehicle,
        TacticalUnitClass.Apc => Apc,
        TacticalUnitClass.ArmoredVehicle => ArmoredVehicle,
        TacticalUnitClass.Tank => Tank,
        TacticalUnitClass.AirObject => AirObject,
        _ => Infantry
    };
}

public enum TacticalOrderType
{
    Hold,
    Advance,
    SeizeObjective,
    DefendObjective,
    Support,
    Withdraw
}

public enum TacticalActionState
{
    AwaitingOrders,
    Advancing,
    Searching,
    Engaging,
    SeekingCover,
    Treating,
    Holding,
    Regrouping,
    Incapacitated,
    Disabled
}

public enum BattlePhase
{
    Deployment,
    Advance,
    Contact,
    Engagement,
    Resolution,
    Complete
}

public enum BattleResult
{
    Ongoing,
    BlueVictory,
    RedVictory,
    Draw
}

public enum TacticalEventType
{
    Scenario,
    Order,
    Movement,
    Contact,
    Phase,
    Medical,
    Outcome
}

public sealed class UnitCommandState
{
    public required int UnitId { get; init; }
    public required TacticalOrderType Order { get; set; }
    public required GridPoint Objective { get; set; }
    public Position3D? PreciseObjectiveMeters { get; set; }
    public TacticalActionState Action { get; set; } = TacticalActionState.AwaitingOrders;
    public string StatusText { get; set; } = "Awaiting orders";
    public int? ContactUnitId { get; set; }
    public long LastDecisionTick { get; set; } = -1_000_000;
    public long LastActionChangeTick { get; set; } = -1_000_000;
    public bool ObjectiveReached { get; set; }
}

public enum ObjectiveProgressState
{
    Unreached,
    Approaching,
    Entered,
    EstablishingPresence,
    Clearing,
    Held,
    Secured,
    Contested,
    Lost
}

public sealed class BattleObjective
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    // Coarse compatibility cell retained for legacy UI/scenarios.
    public required GridPoint Position { get; init; }
    // v1.9.3: country-scale objectives use continuous meters for control checks.
    public Position3D? PrecisePositionMeters { get; init; }
    public int CaptureRadiusCells { get; init; } = 1;
    public double? CaptureRadiusMeters { get; init; }
    public int RequiredControlSeconds { get; init; } = 20;
    public int BlueControlSeconds { get; set; }
    public int RedControlSeconds { get; set; }
    public ObjectiveProgressState BlueProgress { get; set; } = ObjectiveProgressState.Unreached;
    public ObjectiveProgressState RedProgress { get; set; } = ObjectiveProgressState.Unreached;
}

public sealed record TacticalEvent(
    long Tick,
    TacticalEventType Type,
    TacticalFaction? Faction,
    int? UnitId,
    string Message);

public sealed class TacticalUnit
{
    public required int Id { get; init; }
    public string EntityKey { get; init; } = "";
    public required TacticalFaction Faction { get; init; }
    // Coarse terrain/nav cell. This is no longer the authoritative entity position.
    public required GridPoint Position { get; set; }
    // Continuous local-world position in meters at ground level. This is authoritative when set.
    public Position3D? PrecisePositionMeters { get; set; }
    // Exact final movement destination inside a coarse navigation cell.
    public Position3D? MovementDestinationMeters { get; set; }
    public TacticalUnitClass UnitClass { get; init; } = TacticalUnitClass.Person;
    public MobilityProfile? MobilityOverride { get; init; }
    public double CurrentSpeedMetersPerSecond { get; set; }
    public double CurrentAccelerationMetersPerSecondSquared { get; set; }
    public MobilityProfile Mobility => MobilityOverride ?? MobilityProfile.For(UnitClass);
    public string DisplayName { get; init; } = "Unit";
    public string? DefinitionId { get; init; }
    public Bounds3D Bounds { get; init; } = Bounds3D.Human;
    public SignatureProfile Signature { get; set; } = SignatureProfile.Human;
    public List<SensorDefinition> Sensors { get; } = [];
    // Typed capability references let scenario entities carry systems whose detailed solver
    // is not active yet without pretending those systems are ordinary rifles.
    public List<string> CapabilityIds { get; } = [];
    public double EyeHeightMeters { get; set; } = 1.7;
    public Orientation3D Orientation { get; set; } = Orientation3D.Zero;
    public bool Alive { get; set; } = true;
    public SoldierRuntime? Soldier { get; set; }
    public bool Selected { get; set; }
    public int SightRange { get; set; } = 8;
    public int MovementProgress { get; set; }
    public List<GridPoint> Path { get; } = [];
    // Meter-native local detours used around synthetic urban geometry. These are physical
    // waypoints, not planning cells.
    public List<Position3D> ContinuousWaypoints { get; } = [];
    public double LocalCohesion01 { get; set; } = 0.75;
    public CohesionBand CohesionBand { get; set; } = CohesionBand.Cohesive;
    public long LastCohesionChangeTick { get; set; } = -1_000_000;

    public Position3D GroundPosition(TacticalState state)
    {
        if (PrecisePositionMeters is { } precise)
            return precise with { Z = state.World.GroundAltitudeAt(precise.X, precise.Y) };
        return state.World.CellCenter(Position, 0);
    }

    public Position3D WorldPosition(TacticalState state)
    {
        var ground = GroundPosition(state);
        return ground with { Z = ground.Z + Math.Max(0, EyeHeightMeters) };
    }

    public void SetGroundPosition(TacticalState state, Position3D position)
    {
        var z = state.World.GroundAltitudeAt(position.X, position.Y);
        PrecisePositionMeters = new Position3D(position.X, position.Y, z);
        Position = state.World.GridPointFromWorld(position.X, position.Y);
        state.MarkSpatialIndexDirty();
    }
}

public sealed class TacticalState
{
    public TacticalState(int width, int height, uint randomSeed = 1, double cellSizeMeters = 100)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        Width = width;
        Height = height;
        Tiles = new TacticalTile[width, height];
        World = new TacticalWorldModel(width, height, cellSizeMeters);
        Random = new DeterministicRng(randomSeed);
        SpatialIndex = new SpatialHashIndex(Math.Clamp(cellSizeMeters, 10, 50));
        Performance = new SimulationPerformanceCounters();
        LodRegistry = new SimulationLodRegistry();
        Intents = new AiIntentStore();
        Journal = new SimulationJournal();
        Infrastructure = new InfrastructureNetwork();
        OperationalSupport = new OperationalSupportState();
        JointSupport = new JointSupportState();
        CommandAndControl = new CommandAndControlState();
        Knowledge = Enum.GetValues<TacticalFaction>()
            .ToDictionary(faction => faction, faction => new FactionKnowledge(faction));
    }

    public int Width { get; }
    public int Height { get; }
    public TacticalTile[,] Tiles { get; }
    public TacticalWorldModel World { get; }
    public DeterministicRng Random { get; }
    public SpatialHashIndex SpatialIndex { get; }
    public SimulationPerformanceCounters Performance { get; }
    public SimulationLodRegistry LodRegistry { get; }
    public AiIntentStore Intents { get; }
    public SimulationJournal Journal { get; }
    public InfrastructureNetwork Infrastructure { get; }
    public OperationalSupportState OperationalSupport { get; }
    public JointSupportState JointSupport { get; }
    public CommandAndControlState CommandAndControl { get; }
    public List<StructureVolume> Structures { get; } = [];
    private readonly Dictionary<int, TacticalUnit> _unitsById = [];
    private bool _spatialIndexDirty = true;
    public Dictionary<TacticalFaction, FactionKnowledge> Knowledge { get; }
    public List<TacticalUnit> Units { get; } = [];
    public long Tick { get; set; }
    public double SecondsPerTick { get; set; } = 1.0;
    public bool AiEnabled { get; set; } = true;
    // Campaign sandboxes continue after local objectives or force collapse. A higher-level
    // political/economic layer may end them later; the tactical engine must not declare a winner.
    public bool OpenEndedScenario { get; set; }
    public string ScenarioName { get; set; } = "Tactical engagement";
    public string MissionBriefing { get; set; } = "No mission briefing loaded.";
    public BattlePhase Phase { get; set; } = BattlePhase.Deployment;
    public BattleResult Result { get; set; } = BattleResult.Ongoing;
    public long? FirstContactTick { get; set; }
    public Dictionary<int, UnitCommandState> UnitCommands { get; } = [];
    public List<BattleObjective> Objectives { get; } = [];
    public List<TacticalEvent> ActivityEvents { get; } = [];
    public EnvironmentState Environment { get; } = EnvironmentState.DayClear();
    public List<CombatEvent> CombatEvents { get; } = [];

    public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;

    public Position3D PositionOf(TacticalUnit unit) => unit.WorldPosition(this);

    public Position3D GroundPositionOf(TacticalUnit unit) => unit.GroundPosition(this);

    public TacticalUnit? UnitById(int unitId) => _unitsById.GetValueOrDefault(unitId);

    public void MarkSpatialIndexDirty() => _spatialIndexDirty = true;

    public void RebuildSpatialIndex()
    {
        _unitsById.Clear();
        foreach (var unit in Units) _unitsById[unit.Id] = unit;
        SpatialIndex.Rebuild(Units, GroundPositionOf);
        _spatialIndexDirty = false;
    }

    public void EnsurePrecisePositions()
    {
        var changed = false;
        foreach (var unit in Units)
        {
            if (unit.PrecisePositionMeters is not null) continue;
            unit.PrecisePositionMeters = World.CellCenter(unit.Position, 0);
            changed = true;
        }
        if (changed) _spatialIndexDirty = true;
        if (_spatialIndexDirty || SpatialIndex.IndexedEntityCount != Units.Count(unit => unit.Alive))
            RebuildSpatialIndex();
    }

    public SpatialContext ContextOf(TacticalUnit unit)
    {
        var position = GroundPositionOf(unit);
        return World.ContextAt(position.X, position.Y);
    }

    public TerritoryState TerritoryFor(TacticalFaction faction, GridPoint point)
    {
        var context = World.Context(point);
        if (context.Territory is TerritoryState.Battlefield or TerritoryState.Contested)
            return context.Territory;
        if (context.ControllingFaction is null || context.ControllingFaction == TacticalFaction.Neutral)
            return TerritoryState.Neutral;
        return context.ControllingFaction == faction
            ? TerritoryState.Friendly
            : TerritoryState.Hostile;
    }

    public UnitCommandState? CommandFor(TacticalUnit unit) => UnitCommands.GetValueOrDefault(unit.Id);

    public void AddActivity(TacticalEventType type, string message,
        TacticalFaction? faction = null, int? unitId = null)
    {
        ActivityEvents.Add(new TacticalEvent(Tick, type, faction, unitId, message));
        var entityKey = unitId is { } id ? UnitById(id)?.EntityKey : null;
        Journal.Append(Tick, type.ToString(), message, entityKey, faction);
        if (ActivityEvents.Count > 2000)
            ActivityEvents.RemoveRange(0, ActivityEvents.Count - 2000);
    }
}
