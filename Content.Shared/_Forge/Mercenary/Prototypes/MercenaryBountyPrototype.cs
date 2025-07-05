using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Forge.Mercenary.Prototypes;

[Prototype, Serializable, NetSerializable]
public sealed partial class MercenaryBountyPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public int Reward;

    [DataField]
    public LocId Description = string.Empty;

    [DataField(required: true)]
    public List<MercenaryBountyItemEntry> Entries = new();

    [DataField]
    public bool SpawnChest = true;

    [DataField]
    public string IdPrefix = "ARR-";
}

[DataDefinition, Serializable, NetSerializable]
public readonly partial record struct MercenaryBountyItemEntry()
{
    [IdDataField]
    public string ID { get; init; } = default!;

    [DataField]
    public int Amount { get; init; } = 1;

    [DataField]
    public LocId Name { get; init; } = string.Empty;
}
