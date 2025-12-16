using Robust.Shared.Serialization;


namespace Content.Shared._NC.Trade;


[Serializable, NetSerializable,]
public sealed class StoreListingData
{
    public string Category = string.Empty;
    public string CurrencyId = string.Empty;
    public string Id = string.Empty;
    public StoreMode Mode;
    public int Owned;
    public int Price;
    public string ProductEntity = string.Empty;
    public int Remaining = -1;

    public StoreListingData() { }

    public StoreListingData(
        string id,
        string productEntity,
        int price,
        string category,
        string currencyId,
        StoreMode mode,
        int owned = 0,
        int remaining = -1
    )
    {
        Id = id;
        ProductEntity = productEntity;
        Price = price;
        Category = category;
        CurrencyId = currencyId;
        Mode = mode;
        Owned = owned;
        Remaining = remaining;
    }
}

[Serializable]
public sealed class ContractTargetServerData
{
    public string TargetItem { get; set; } = string.Empty;
    public int Required { get; set; }
    public int Progress { get; set; }

    [DataField("match")]
    public PrototypeMatchMode MatchMode = PrototypeMatchMode.Exact;

}

[Serializable]
public sealed class ContractServerData
{
    public List<ContractTargetServerData> Targets { get; set; } = new();


    public string TargetItem { get; set; } = string.Empty;
    public int Required { get; set; }
    public int Progress { get; set; }

    [DataField("match")]
    public PrototypeMatchMode MatchMode = PrototypeMatchMode.Exact;


    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    public int Reward { get; set; }
    public string RewardCurrency { get; set; } = string.Empty;
    public string? RewardItem { get; set; }
    public int RewardItemCount { get; set; }


    public Dictionary<string, int> RewardCurrencies { get; set; } = new();
    public Dictionary<string, int> RewardItems { get; set; } = new();

    public string Difficulty { get; set; } = "Easy";
    public string Description { get; set; } = string.Empty;

    public bool Completed
    {
        get
        {
            if (Targets.Count > 0)
            {
                var any = false;
                foreach (var t in Targets)
                {
                    if (t.Required <= 0)
                        continue;

                    any = true;
                    if (t.Progress < t.Required)
                        return false;
                }

                return any;
            }

            return Required > 0 && Progress >= Required;
        }
    }
}

[Serializable, NetSerializable,]
public sealed class ContractTargetClientData
{
    public ContractTargetClientData() { }

    public ContractTargetClientData(string targetItem, int required, int progress)
    {
        TargetItem = targetItem;
        Required = required;
        Progress = progress;
    }

    [DataField("match")]
    public PrototypeMatchMode MatchMode = PrototypeMatchMode.Exact;

    public string TargetItem { get; set; } = string.Empty;
    public int Required { get; set; }
    public int Progress { get; set; }
}

[Serializable, NetSerializable,]
public enum StoreMode
{
    Buy,
    Sell,
    Exchange
}
