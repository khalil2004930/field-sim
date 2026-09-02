namespace FieldSim.Core;

/// <summary>
/// High-level simulation intent. These describe goals, not real-world procedural tactics.
/// Lower AI layers are free to translate intent into safe game-state actions.
/// </summary>
public enum AiIntentType
{
    HoldArea,
    MoveToArea,
    ObserveArea,
    Protect,
    Support,
    Withdraw,
    Regroup,
    Resupply,
    TreatCasualties,
    AwaitOrders
}

public enum AiCommandLevel
{
    Faction,
    Formation,
    SubFormation,
    Entity
}

public sealed record SimulationAreaIntent(
    Position3D CenterMeters,
    double RadiusMeters);

public sealed class AiIntent
{
    public required string Id { get; init; }
    public required string IssuerStableKey { get; init; }
    public required string RecipientStableKey { get; init; }
    public AiCommandLevel CommandLevel { get; init; }
    public AiIntentType Type { get; init; }
    public SimulationAreaIntent? Area { get; init; }
    public int Priority { get; init; } = 50;
    public long IssuedTick { get; init; }
    public long? ReceivedTick { get; set; }
    public string? ParentIntentId { get; init; }
    public string Status { get; set; } = "Issued";
}

public sealed class AiIntentStore
{
    private readonly Dictionary<string, AiIntent> _intents = new(StringComparer.Ordinal);

    public IReadOnlyCollection<AiIntent> Intents => _intents.Values;

    public void Upsert(AiIntent intent) => _intents[intent.Id] = intent;

    public IEnumerable<AiIntent> ForRecipient(string stableKey) =>
        _intents.Values.Where(intent => string.Equals(intent.RecipientStableKey, stableKey, StringComparison.Ordinal));
}
