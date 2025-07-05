using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;
using Content.Shared._Forge.Mercenary.Prototypes;

namespace Content.Shared._Forge.Mercenary;

[DataDefinition, NetSerializable, Serializable]
public readonly partial record struct MercenaryBountyData
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Id { get; init; } = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField(required: true)]
    public ProtoId<MercenaryBountyPrototype> Bounty { get; init; } = string.Empty;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool Accepted { get; init; } = false;

    public MercenaryBountyData(MercenaryBountyPrototype bounty, int uniqueIdentifier, bool accepted)
    {
        Bounty = bounty.ID;
        Id = $"{bounty.IdPrefix}{uniqueIdentifier:D3}";
        Accepted = accepted;
    }

    public MercenaryBountyData(MercenaryBountyPrototype bounty, string id, bool accepted)
    {
        Bounty = bounty.ID;
        Id = id;
        Accepted = accepted;
    }
}
