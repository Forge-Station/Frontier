using Robust.Shared.Serialization;
using Robust.Shared.Prototypes;
using Content.Shared._Forge.Miners.Prototypes;

namespace Content.Shared._Forge.Miners;

[DataDefinition, NetSerializable, Serializable]
public readonly partial record struct MinersBountyData
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string Id { get; init; } = string.Empty;

    [ViewVariables(VVAccess.ReadWrite)]
    [DataField(required: true)]
    public ProtoId<MinersBountyPrototype> Bounty { get; init; } = string.Empty;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool Accepted { get; init; } = false;

    public MinersBountyData(MinersBountyPrototype bounty, int uniqueIdentifier, bool accepted)
    {
        Bounty = bounty.ID;
        Id = $"{bounty.IdPrefix}{uniqueIdentifier:D3}";
        Accepted = accepted;
    }

    public MinersBountyData(MinersBountyPrototype bounty, string id, bool accepted)
    {
        Bounty = bounty.ID;
        Id = id;
        Accepted = accepted;
    }
}
