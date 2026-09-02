namespace FieldSim.Core;

public sealed record SimulationJournalEntry(
    long Sequence,
    long Tick,
    string Category,
    string Message,
    string? EntityKey,
    TacticalFaction? Faction);

/// <summary>
/// Bounded, deterministic event journal intended to become the replay/AAR backbone. It stores
/// semantic events rather than UI strings owned by a particular client.
/// </summary>
public sealed class SimulationJournal
{
    private readonly List<SimulationJournalEntry> _entries = [];
    private long _nextSequence = 1;

    public int Capacity { get; }
    public IReadOnlyList<SimulationJournalEntry> Entries => _entries;

    public SimulationJournal(int capacity = 10_000)
    {
        Capacity = Math.Max(100, capacity);
    }

    public SimulationJournalEntry Append(
        long tick,
        string category,
        string message,
        string? entityKey = null,
        TacticalFaction? faction = null)
    {
        var entry = new SimulationJournalEntry(_nextSequence++, tick, category, message, entityKey, faction);
        _entries.Add(entry);
        if (_entries.Count > Capacity)
            _entries.RemoveRange(0, _entries.Count - Capacity);
        return entry;
    }

    public IReadOnlyList<SimulationJournalEntry> Since(long sequenceExclusive, int maximum = 500) =>
        _entries.Where(entry => entry.Sequence > sequenceExclusive)
            .Take(Math.Clamp(maximum, 1, 5000))
            .ToArray();

    public void Clear()
    {
        _entries.Clear();
        _nextSequence = 1;
    }
}
