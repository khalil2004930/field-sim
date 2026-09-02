namespace FieldSim.Core;

public sealed class TacticalWorldModel
{
    private readonly double[,] _elevationMeters;
    private readonly SpatialContext[,] _contexts;

    public TacticalWorldModel(int width, int height, double cellSizeMeters)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (cellSizeMeters <= 0) throw new ArgumentOutOfRangeException(nameof(cellSizeMeters));

        Width = width;
        Height = height;
        CellSizeMeters = cellSizeMeters;
        _elevationMeters = new double[width, height];
        _contexts = new SpatialContext[width, height];

        for (var y = 0; y < height; y++)
        for (var x = 0; x < width; x++)
        {
            _contexts[x, y] = new SpatialContext(
                TerrainType.Open,
                AreaType.OpenTerrain,
                TerritoryState.Neutral,
                0,
                0,
                0,
                0.05,
                0.02,
                0);
        }
    }

    public int Width { get; }
    public int Height { get; }
    public double CellSizeMeters { get; }

    /// <summary>
    /// Optional external physical-world provider. Unity can own terrain, LOS, structures,
    /// walkability and path queries while the existing FieldSim world remains the fallback.
    /// </summary>
    public IExternalEnvironmentQueryProvider? ExternalQueries { get; set; }

    public bool InBounds(GridPoint point) =>
        point.X >= 0 && point.X < Width && point.Y >= 0 && point.Y < Height;

    public void SetCell(GridPoint point, double groundAltitudeMeters, SpatialContext context)
    {
        if (!InBounds(point)) throw new ArgumentOutOfRangeException(nameof(point));
        _elevationMeters[point.X, point.Y] = groundAltitudeMeters;
        _contexts[point.X, point.Y] = context.WithGroundAltitude(groundAltitudeMeters);
    }

    public double GroundAltitude(GridPoint point)
    {
        if (!InBounds(point)) throw new ArgumentOutOfRangeException(nameof(point));
        return _elevationMeters[point.X, point.Y];
    }

    public SpatialContext Context(GridPoint point)
    {
        if (!InBounds(point)) throw new ArgumentOutOfRangeException(nameof(point));
        return _contexts[point.X, point.Y];
    }

    public Position3D CellCenter(GridPoint point, double heightAboveGroundMeters = 0)
    {
        if (!InBounds(point)) throw new ArgumentOutOfRangeException(nameof(point));
        var x = (point.X + 0.5) * CellSizeMeters;
        // Screen/grid Y increases downward. World Y increases northward.
        var y = (Height - point.Y - 0.5) * CellSizeMeters;
        return new Position3D(x, y, GroundAltitudeAt(x, y) + heightAboveGroundMeters);
    }

    public GridPoint GridPointFromWorld(double xMeters, double yMeters)
    {
        var x = Math.Clamp((int)Math.Floor(xMeters / CellSizeMeters), 0, Width - 1);
        var northIndex = Math.Clamp((int)Math.Floor(yMeters / CellSizeMeters), 0, Height - 1);
        var y = Height - 1 - northIndex;
        return new GridPoint(x, y);
    }

    public double GroundAltitudeAt(double xMeters, double yMeters)
    {
        if (ExternalQueries is { } external &&
            external.TryGetGroundAltitude(xMeters, yMeters, out var externalAltitude) &&
            double.IsFinite(externalAltitude))
        {
            return externalAltitude;
        }

        var point = GridPointFromWorld(xMeters, yMeters);
        return GroundAltitude(point);
    }

    public SpatialContext ContextAt(double xMeters, double yMeters)
    {
        var point = GridPointFromWorld(xMeters, yMeters);
        return Context(point).WithGroundAltitude(GroundAltitudeAt(xMeters, yMeters));
    }

    public bool TryExternalLineOfSight(
        Position3D observer,
        Position3D target,
        out LineOfSightResult result)
    {
        if (ExternalQueries is { } external &&
            external.TryEvaluateLineOfSight(observer, target, out result) &&
            result is not null)
        {
            return true;
        }

        result = null!;
        return false;
    }

    public bool TryExternalPointInsideStructure(Position3D point, out bool insideStructure)
    {
        if (ExternalQueries is { } external &&
            external.TryIsPointInsideStructure(point, out insideStructure))
        {
            return true;
        }

        insideStructure = false;
        return false;
    }

    public bool TryExternalWalkability(
        Position3D point,
        TacticalUnitClass unitClass,
        out bool walkable)
    {
        if (ExternalQueries is { } external &&
            external.TryIsWalkable(point, unitClass, out walkable))
        {
            return true;
        }

        walkable = false;
        return false;
    }

    public bool TryExternalMovementBlock(
        Position3D from,
        Position3D to,
        TacticalUnitClass unitClass,
        out bool blocked)
    {
        if (ExternalQueries is { } external &&
            external.TryIsMovementSegmentBlocked(from, to, unitClass, out blocked))
        {
            return true;
        }

        blocked = false;
        return false;
    }

    public bool TryExternalPath(
        Position3D from,
        Position3D to,
        TacticalUnitClass unitClass,
        out IReadOnlyList<Position3D> waypoints)
    {
        if (ExternalQueries is { } external &&
            external.TryFindPath(from, to, unitClass, out waypoints) &&
            waypoints is not null)
        {
            return true;
        }

        waypoints = Array.Empty<Position3D>();
        return false;
    }
}
