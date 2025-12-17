using Robust.Shared.Serialization;


namespace Content.Shared._NC.Trade;


[Serializable, NetSerializable,]
public enum StoreUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class StoreUiState(
    int revision,
    int balance,
    Dictionary<string, int> balanceByCurrency,
    List<StoreListingData> listings,
    Dictionary<string, int> massSellTotals,
    List<ContractClientData> contracts)
    : BoundUserInterfaceState
{
    public int Revision = revision;

    public int Balance = balance;
    public Dictionary<string, int> BalanceByCurrency = balanceByCurrency;

    public List<StoreListingData> Listings = listings;
    public Dictionary<string, int> MassSellTotals = massSellTotals;
    public List<ContractClientData> Contracts = contracts;
}


[Serializable, NetSerializable,]
public sealed class ContractUiState(List<ContractClientData> contracts) : BoundUserInterfaceState
{
    public List<ContractClientData> Contracts { get; } = contracts;
}

[Serializable, NetSerializable,]
public sealed class ContractClientData
{
    public bool Completed;
    public string Description;
    public string Difficulty;
    public string Id;
    public string Name;
    public int Progress;
    public int Required;

    public int Reward;

    public Dictionary<string, int> RewardCurrencies = new();
    public string RewardCurrency;
    public string? RewardItem;
    public int RewardItemCount;
    public Dictionary<string, int> RewardItems = new();
    public string TargetItem;

    public List<ContractTargetClientData> Targets;

    public ContractClientData()
    {
        Targets = new();
        Id = string.Empty;
        Name = string.Empty;
        TargetItem = string.Empty;
        RewardCurrency = string.Empty;
        Difficulty = "Easy";
        Description = string.Empty;
    }

    public ContractClientData(
        string id,
        string name,
        string targetItem,
        int required,
        int progress,
        int reward,
        string rewardCurrency,
        string? rewardItem,
        int rewardItemCount,
        string difficulty,
        bool completed,
        string description,
        List<ContractTargetClientData>? targets = null
    )
    {
        Id = id;
        Name = name;
        TargetItem = targetItem;
        Required = required;
        Progress = progress;
        Reward = reward;
        RewardCurrency = rewardCurrency;
        RewardItem = rewardItem;
        RewardItemCount = rewardItemCount;
        Difficulty = difficulty;
        Completed = completed;
        Description = description;
        Targets = targets ?? new();
    }
}

[Serializable, NetSerializable,]
public sealed class StoreBuyListingBoundUiMessage(string listingId, int count) : BoundUserInterfaceMessage
{
    public string ListingId { get; } = listingId;
    public int Count { get; } = count;
}

[Serializable, NetSerializable,]
public sealed class StoreSellListingBoundUiMessage(string listingId, int count) : BoundUserInterfaceMessage
{
    public string ListingId { get; } = listingId;
    public int Count { get; } = count;
}

[Serializable, NetSerializable,]
public sealed class StoreMassSellPulledCrateBoundUiMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable,]
public sealed class RequestUiRefreshMessage : BoundUserInterfaceMessage { }

[Serializable, NetSerializable,]
public sealed class ClaimContractBoundMessage(string id) : BoundUserInterfaceMessage
{
    public string ContractId { get; } = id;
}

[Serializable, NetSerializable,]
public sealed class RequestContractsRefreshMessage : BoundUserInterfaceMessage { }
