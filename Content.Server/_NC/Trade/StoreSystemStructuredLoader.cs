using System.Linq;
using Content.Shared._NC.Trade;
using Robust.Shared.Prototypes;


namespace Content.Server._NC.Trade;


public sealed class StoreSystemStructuredLoader : EntitySystem
{
    private static readonly ISawmill Sawmill = Logger.GetSawmill("ncstore-loader");

    [Dependency] private readonly NcContractSystem _contracts = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NcStoreComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NcStoreComponent, ComponentStartup>(OnStartup);
    }

    private void OnMapInit(EntityUid uid, NcStoreComponent comp, MapInitEvent args)
    {
        if (comp.Listings.Count == 0)
            TryLoadPresets(uid, comp, "MapInit");

        _contracts.InitContractsForStore(uid, comp);
    }

    private void OnStartup(EntityUid uid, NcStoreComponent comp, ComponentStartup args)
    {
        if (comp.Listings.Count == 0)
            TryLoadPresets(uid, comp, "Startup");

        _contracts.InitContractsForStore(uid, comp);
    }

    private void TryLoadPresets(EntityUid uid, NcStoreComponent comp, string reason)
    {
        if (comp.BuyPresets.Count == 0 &&
            comp.SellPresets.Count == 0 &&
            !string.IsNullOrWhiteSpace(comp.LegacyPreset))
            comp.BuyPresets.Add(comp.LegacyPreset!);

        if (comp.BuyPresets.Count == 0 && comp.SellPresets.Count == 0)
        {
            Sawmill.Warning($"[NcStore] {ToPrettyString(uid)}: нет ни одного пресета (reason={reason})");
            return;
        }

        comp.CurrencyWhitelist.Clear();
        comp.Categories.Clear();
        comp.Listings.Clear();

        var total = 0;

        foreach (var id in comp.BuyPresets)
            total += LoadPresetForMode(id, StoreMode.Buy, comp);

        foreach (var id in comp.SellPresets)
            total += LoadPresetForMode(id, StoreMode.Sell, comp);

        if (total == 0)
        {
            Sawmill.Warning($"[NcStore] {ToPrettyString(uid)}: ни одного лота не загружено (reason={reason})");
            return;
        }

        Sawmill.Info(
            $"[NcStore] {ToPrettyString(uid)}: загружено {total} лотов. " +
            $"BuyPresets=[{string.Join(", ", comp.BuyPresets)}], " +
            $"SellPresets=[{string.Join(", ", comp.SellPresets)}], reason={reason}");
    }

    private int LoadPresetForMode(string presetId, StoreMode mode, NcStoreComponent comp)
    {
        if (!_prototypes.TryIndex<StorePresetStructuredPrototype>(presetId, out var preset))
        {
            Sawmill.Error($"[NcStore] Пресет '{presetId}' не найден");
            return 0;
        }

        var count = 0;

        if (!string.IsNullOrWhiteSpace(preset.Currency) &&
            !comp.CurrencyWhitelist.Contains(preset.Currency))
            comp.CurrencyWhitelist.Add(preset.Currency);

        foreach (var (category, entries) in preset.Catalog)
        {
            if (!comp.Categories.Contains(category))
                comp.Categories.Add(category);

            foreach (var entry in entries)
            {
                var baseId = $"{presetId}:{mode}:{category}:{entry.Proto}";

                var id = baseId;
                var suffix = 1;
                while (comp.Listings.Any(l => l.Id == id))
                    id = $"{baseId}#{suffix++}";

                var listing = new StoreListingPrototype
                {
                    Id = id,
                    ProductEntity = entry.Proto,
                    MatchMode = entry.MatchMode,
                    Mode = mode,
                    Categories = new() { category, },
                    Conditions = new(),
                    RemainingCount = entry.Count ?? -1,
                    Cost = new()
                };

                if (!string.IsNullOrWhiteSpace(preset.Currency))
                    listing.Cost[preset.Currency] = entry.Price;

                comp.Listings.Add(listing);
                count++;
            }
        }

        return count;
    }
}
