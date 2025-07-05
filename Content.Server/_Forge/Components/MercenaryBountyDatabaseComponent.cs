using Content.Shared._Forge.Mercenary;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server._Forge.Mercenary.Components;

[RegisterComponent]
public sealed partial class MercenaryBountyDatabaseComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int MaxBounties = 6;

    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public List<MercenaryBountyData> Bounties = new();

    [DataField]
    public int TotalBounties;

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextSkipTime = TimeSpan.Zero;

    [DataField]
    public TimeSpan SkipDelay = TimeSpan.FromMinutes(15);

    [DataField]
    public TimeSpan CancelDelay = TimeSpan.FromMinutes(30);
}
