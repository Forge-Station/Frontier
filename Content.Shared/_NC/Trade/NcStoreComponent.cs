using Robust.Shared.GameStates;


namespace Content.Shared._NC.Trade;


[RegisterComponent, NetworkedComponent,]
public sealed partial class NcStoreComponent : Component
{
    public EntityUid? CurrentUser;
    public int UiRevision;
    [DataField("categories")]
    public List<string> Categories { get; set; } = new();

    [DataField("currencyWhitelist")]
    public List<string> CurrencyWhitelist { get; set; } = new();

    public List<StoreListingPrototype> Listings { get; set; } = new();

    [DataField("preset")]
    public string? LegacyPreset { get; set; }

    [DataField("buyPresets")]
    public List<string> BuyPresets { get; set; } = new();

    [DataField("sellPresets")]
    public List<string> SellPresets { get; set; } = new();

    [DataField("contractPresets")]
    public List<string> ContractPresets { get; set; } = new();

    [DataField("contracts")]
    public string? LegacyContractsPreset { get; set; }

    public Dictionary<string, ContractServerData> Contracts { get; } = new();

    [DataField("rewardCurrencies")]
    public Dictionary<string, int> RewardCurrencies { get; set; } = new();

    [DataField("rewardItems")]
    public Dictionary<string, int> RewardItems { get; set; } = new();
}
