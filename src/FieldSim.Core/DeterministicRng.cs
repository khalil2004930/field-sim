namespace FieldSim.Core;

public sealed class DeterministicRng
{
    private uint _state;

    public DeterministicRng(uint seed) => _state = seed == 0 ? 1u : seed;

    public uint State => _state;

    public uint NextUInt32()
    {
        var value = _state;
        value ^= value << 13;
        value ^= value >> 17;
        value ^= value << 5;
        _state = value;
        return value;
    }

    public double NextDouble()
    {
        // Deterministic value in the half-open interval [0, 1).
        // Use 2^32 as the divisor so uint.MaxValue never maps to exactly 1.0.
        return NextUInt32() / 4294967296.0;
    }

    public int NextInclusive(int minimum, int maximum)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(minimum, maximum);
        var span = (uint)(maximum - minimum + 1);
        return minimum + (int)(NextUInt32() % span);
    }
}
