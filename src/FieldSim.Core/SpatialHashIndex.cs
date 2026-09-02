namespace FieldSim.Core;

/// <summary>
/// Lightweight broad-phase index for dynamic simulation entities. Bucket size is an
/// implementation detail only: entity positions remain continuous world coordinates.
/// </summary>
public sealed class SpatialHashIndex
{
    private readonly Dictionary<(int X, int Y), List<int>> _buckets = [];
    private readonly Dictionary<int, Position3D> _positions = [];

    public SpatialHashIndex(double bucketSizeMeters = 25)
    {
        if (bucketSizeMeters <= 0) throw new ArgumentOutOfRangeException(nameof(bucketSizeMeters));
        BucketSizeMeters = bucketSizeMeters;
    }

    public double BucketSizeMeters { get; }
    public int IndexedEntityCount => _positions.Count;

    public void Clear()
    {
        _buckets.Clear();
        _positions.Clear();
    }

    public void Rebuild(IEnumerable<TacticalUnit> units, Func<TacticalUnit, Position3D> positionSelector)
    {
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(positionSelector);
        Clear();
        foreach (var unit in units)
        {
            if (!unit.Alive) continue;
            Add(unit.Id, positionSelector(unit));
        }
    }

    public void Add(int entityId, Position3D position)
    {
        _positions[entityId] = position;
        var key = Key(position.X, position.Y);
        if (!_buckets.TryGetValue(key, out var bucket))
        {
            bucket = [];
            _buckets[key] = bucket;
        }
        bucket.Add(entityId);
    }

    public IReadOnlyList<int> QueryRadius(Position3D center, double radiusMeters)
    {
        if (radiusMeters < 0) throw new ArgumentOutOfRangeException(nameof(radiusMeters));
        if (_positions.Count == 0) return [];

        var minX = BucketCoordinate(center.X - radiusMeters);
        var maxX = BucketCoordinate(center.X + radiusMeters);
        var minY = BucketCoordinate(center.Y - radiusMeters);
        var maxY = BucketCoordinate(center.Y + radiusMeters);
        var radiusSquared = radiusMeters * radiusMeters;
        var result = new List<int>();

        for (var by = minY; by <= maxY; by++)
        for (var bx = minX; bx <= maxX; bx++)
        {
            if (!_buckets.TryGetValue((bx, by), out var bucket)) continue;
            foreach (var entityId in bucket)
            {
                if (!_positions.TryGetValue(entityId, out var position)) continue;
                var dx = position.X - center.X;
                var dy = position.Y - center.Y;
                if (dx * dx + dy * dy <= radiusSquared)
                    result.Add(entityId);
            }
        }

        return result;
    }

    public Position3D? PositionOf(int entityId) =>
        _positions.TryGetValue(entityId, out var position) ? position : null;

    private (int X, int Y) Key(double x, double y) =>
        (BucketCoordinate(x), BucketCoordinate(y));

    private int BucketCoordinate(double value) =>
        (int)Math.Floor(value / BucketSizeMeters);
}
