using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;


namespace Content.Shared._NC.Trade;


[Serializable, NetSerializable, Prototype("ncStoreListing"),]
public sealed class StoreListingPrototype : IPrototype
{
    [IdDataField]
    public string Id = string.Empty;

    [DataField("match")]
    public PrototypeMatchMode MatchMode = PrototypeMatchMode.Exact;

    [DataField("mode")]
    public StoreMode Mode = StoreMode.Buy;

    [DataField("productEntity")]
    public string ProductEntity = string.Empty;

    [DataField("cost")]
    public Dictionary<string, int> Cost { get; set; } = new();

    [DataField("categories")]
    public List<string> Categories { get; set; } = new();

    [DataField("conditions")]
    public List<ListingConditionPrototype> Conditions { get; set; } = new();

    [ViewVariables(VVAccess.ReadWrite)]
    public int RemainingCount { get; set; } = -1;

    public string ID => Id;
}

[Prototype("storePresetStructured")]
public sealed partial class StorePresetStructuredPrototype : IPrototype
{
    [DataField("catalog", required: true)]
    public Dictionary<string, List<StoreCatalogEntry>> Catalog = new();

    [DataField("currency", required: true)]
    public string Currency = string.Empty;

    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataDefinition]
    public sealed partial class StoreCatalogEntry
    {
        [DataField("match")]
        public PrototypeMatchMode MatchMode = PrototypeMatchMode.Exact;

        [DataField("price", required: true)]
        public int Price;

        [DataField("proto", required: true)]
        public string Proto = string.Empty;

        [DataField("count")]
        public int? Count { get; set; }
    }
}

[Prototype("storeContract")]
public sealed class StoreContractPrototype : IPrototype
{
    [DataField("match")]
    public PrototypeMatchMode MatchMode = PrototypeMatchMode.Exact;

    [DataField("name")]
    public string Name { get; private set; } = string.Empty;

    [DataField("description")]
    public string Description { get; private set; } = string.Empty;

    [DataField("difficulty")]
    public string Difficulty { get; private set; } = "Easy";

    [DataField("targetItem")]
    public string? TargetItem { get; private set; }

    [DataField("required")]
    public IntRange Required { get; private set; } = IntRange.Fixed(0);

    [DataField("targets")]
    public List<StoreContractTargetEntry>? Targets { get; private set; }

    [DataField("currencies")]
    public List<StoreContractCurrencyRange>? Currencies { get; private set; }

    [DataField("rewardCurrencies")]
    public Dictionary<string, int>? RewardCurrencies { get; private set; }

    [DataField("fixedRewardItems")]
    public Dictionary<string, int>? FixedRewardItems { get; private set; }

    [DataField("rewardItems")]
    public List<StoreContractBonusReward>? RewardItems { get; private set; }

    [DataField("targetCount")]
    public IntRange TargetCount { get; private set; } = IntRange.Fixed(1);

    [DataField("bonusPickCount")]
    public IntRange BonusPickCount { get; private set; } = IntRange.Fixed(1);

    [DataField("reward")]
    public int Reward { get; private set; }

    [DataField("rewardCurrency")]
    public string RewardCurrency { get; private set; } = string.Empty;

    [DataField("rewardItem")]
    public string? RewardItem { get; private set; }

    [DataField("rewardItemCount")]
    public int RewardItemCount { get; private set; }

    [IdDataField]
    public string ID { get; private set; } = default!;
}

[Prototype("storeContractsPreset")]
public sealed class StoreContractsPresetPrototype : IPrototype
{
    [DataField("contracts", required: true)]
    public List<string> Contracts { get; set; } = new();

    [IdDataField]
    public string ID { get; private set; } = default!;
}

[DataDefinition]
public sealed partial class StoreContractTargetEntry
{
    [DataField("id", required: true)]
    public string TargetItemId { get; set; } = default!;

    [DataField("required")]
    public IntRange Required { get; set; } = IntRange.Fixed(0);

    [DataField("weight")]
    public int Weight { get; set; } = 1;
}

public enum StoreContractBonusMode
{
    Add,

    Replace
}

[Serializable, NetSerializable,]
public enum PrototypeMatchMode : byte
{
    Exact = 0,
    Descendants = 1
}

[DataDefinition]
public sealed partial class StoreContractBonusReward
{
    [DataField("count")]
    public int Count = 1;

    [DataField("currencies")]
    public Dictionary<string, int>? RewardCurrencies;

    [DataField("items")]
    public Dictionary<string, int>? RewardItems;

    [DataField("id")]
    public string? Id { get; set; }

    [DataField("pool")]
    public string? PoolId { get; set; }

    [DataField("always")]
    public bool Always { get; set; }

    [DataField("weight")]
    public int Weight { get; set; } = 1;

    [DataField("mode")]
    public StoreContractBonusMode Mode { get; set; } = StoreContractBonusMode.Add;
}

[DataDefinition]
public sealed partial class StoreContractCurrencyRange
{
    [DataField("id", required: true)]
    public string Id { get; set; } = string.Empty;

    [DataField("min")]
    public int Min { get; set; }

    [DataField("max")]
    public int Max { get; set; }
}

[Prototype("ncContractRewardPool")]
public sealed class NcContractRewardPoolPrototype : IPrototype
{
    [DataField("entries")]
    public List<StoreContractBonusReward> Entries { get; private set; } = new();

    [IdDataField]
    public string ID { get; private set; } = default!;
}

[Serializable]
public sealed class ListingConditionPrototype
{
    [DataField("condition")]
    public object? Condition;
}
