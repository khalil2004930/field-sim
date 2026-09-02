namespace FieldSim.Domain;

/// <summary>
/// Cartridge identity is intentionally separate from weapon identity and from any future
/// projectile/terminal-effect profile. v1.7 stores only normalized family metadata.
/// </summary>
public sealed class CartridgeFamilyDataset
{
    public required string Version { get; init; }
    public required string Scope { get; init; }
    public required List<CartridgeFamilyDefinition> Cartridges { get; init; }
}

public sealed class CartridgeFamilyDefinition
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public required string Family { get; init; }
    public List<string> Aliases { get; init; } = [];
    public string? Notes { get; init; }
    public string? SimulationProjectileProfileId { get; init; }
}
