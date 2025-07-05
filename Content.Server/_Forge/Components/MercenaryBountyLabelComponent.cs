using Content.Server.Station.Systems;

namespace Content.Server._Forge.Mercenary.Components;

/// <summary>
/// This is used for marking containers as
/// containing goods for fulfilling bounties.
/// </summary>
[RegisterComponent]
public sealed partial class MercenaryBountyLabelComponent : Component
{
    /// <summary>
    /// The ID for the bounty this label corresponds to.
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Id = string.Empty;

    /// <summary>
    /// Used to prevent recursion in calculating the price.
    /// </summary>
    public bool Calculating;
}
