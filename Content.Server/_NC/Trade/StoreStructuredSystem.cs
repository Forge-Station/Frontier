using System.Linq;
using Content.Server.Popups;
using Content.Server.Storage.Components;
using Content.Shared._NC.Trade;
using Content.Shared.Access.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Stacks;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Timing;


namespace Content.Server._NC.Trade;


public sealed class StoreStructuredSystem : EntitySystem
{
    private const float AutoCloseDistance = 3f;
    private const float CheckInterval = 0.2f;
    [Dependency] private readonly NcContractSystem _contracts = default!;
    [Dependency] private readonly NcStoreLogicSystem _logic = default!;
    private readonly HashSet<EntityUid> _openStoreUids = new();
    [Dependency] private readonly PopupSystem _popups = default!;
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _storesByWatchedRoot = new();
    [Dependency] private readonly NcStoreSystem _storeSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    private readonly Dictionary<EntityUid, (EntityUid User, EntityUid? Crate)> _watchByStore = new();
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    private TimeSpan _nextCheck = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NcStoreComponent, ActivatableUIOpenAttemptEvent>(
            OnUiOpenAttempt,
            new[] { typeof(ActivatableUISystem), });
        SubscribeLocalEvent<NcStoreComponent, BoundUIClosedEvent>(OnUiClosed);
        SubscribeLocalEvent<NcStoreComponent, RequestUiRefreshMessage>(OnUiRefreshRequest);
        SubscribeLocalEvent<AccessReaderComponent, AccessReaderConfigurationChangedEvent>(OnAccessReaderChanged);
        SubscribeLocalEvent<ContainerManagerComponent, EntInsertedIntoContainerMessage>(OnUserEntInserted);
        SubscribeLocalEvent<ContainerManagerComponent, EntRemovedFromContainerMessage>(OnUserEntRemoved);
        SubscribeLocalEvent<StackComponent, StackCountChangedEvent>(OnStackCountChanged);
        SubscribeLocalEvent<NcStoreComponent, ClaimContractBoundMessage>(OnClaimContract);
    }

    private void OnUiOpenAttempt(EntityUid uid, NcStoreComponent comp, ref ActivatableUIOpenAttemptEvent ev)
    {
        ev.Cancel();
        var user = ev.User;

        if (!_ui.HasUi(uid, StoreUiKey.Key))
            return;

        if (!_storeSystem.CanUseStore(uid, comp, user))
            return;

        if (comp.CurrentUser is { } current && current != user)
            return;

        if (TryComp(uid, out TransformComponent? storeXform) &&
            TryComp(user, out TransformComponent? userXform) &&
            !_xform.InRange(storeXform.Coordinates, userXform.Coordinates, AutoCloseDistance))
            return;

        var wasInUse = comp.CurrentUser != null;
        comp.CurrentUser = user;

        if (!wasInUse)
            _openStoreUids.Add(uid);

        EntityUid? crateUid = null;
        if (TryGetPulledClosedCrate(user, out var pulledCrate))
            crateUid = pulledCrate;

        UpdateStoreWatch(uid, user, crateUid);


        if (!_ui.IsUiOpen(uid, StoreUiKey.Key, user))
            _ui.OpenUi(uid, StoreUiKey.Key, user);

        UpdateUiState(uid, comp, user);
    }

    private void OnUiClosed(EntityUid uid, NcStoreComponent comp, BoundUIClosedEvent ev)
    {
        if (!ev.UiKey.Equals(StoreUiKey.Key))
            return;

        comp.CurrentUser = null;
        _openStoreUids.Remove(uid);
        UnregisterStoreWatch(uid);
    }

    private void OnUiRefreshRequest(EntityUid uid, NcStoreComponent comp, RequestUiRefreshMessage msg)
    {
        if (comp.CurrentUser is not { } user)
        {
            _openStoreUids.Remove(uid);
            UnregisterStoreWatch(uid);
            return;
        }

        if (!_storeSystem.CanUseStore(uid, comp, user))
        {
            _ui.CloseUi(uid, StoreUiKey.Key, user);
            comp.CurrentUser = null;
            _openStoreUids.Remove(uid);
            UnregisterStoreWatch(uid);
            return;
        }

        UpdateUiState(uid, comp, user);
    }

    private bool EnsureCrateWatchUpToDate(EntityUid storeUid, EntityUid user)
    {
        EntityUid? crateUid = null;
        if (TryGetPulledClosedCrate(user, out var pulledCrate))
            crateUid = pulledCrate;

        if (_watchByStore.TryGetValue(storeUid, out var prev))
        {
            if (prev.User == user && prev.Crate == crateUid)
                return false;
        }

        UpdateStoreWatch(storeUid, user, crateUid);
        return true;
    }

    public void UpdateUiState(EntityUid uid, NcStoreComponent comp, EntityUid user)
    {
        if (!_storeSystem.CanUseStore(uid, comp, user))
        {
            _ui.CloseUi(uid, StoreUiKey.Key, user);
            comp.CurrentUser = null;
            _openStoreUids.Remove(uid);
            UnregisterStoreWatch(uid);
            return;
        }

        EntityUid? crateUid = null;
        if (TryGetPulledClosedCrate(user, out var pulledCrate))
            crateUid = pulledCrate;

        UpdateStoreWatch(uid, user, crateUid);

        _logic.InvalidateInventoryCache(user);
        if (crateUid is { } cInv)
            _logic.InvalidateInventoryCache(cInv);

        var userSnap = _logic.BuildInventorySnapshot(user);

        NcStoreLogicSystem.InventorySnapshot? crateSnap = null;
        if (crateUid is { } crateEntity)
            crateSnap = _logic.BuildInventorySnapshot(crateEntity);

        UpdateContractsProgress(comp, userSnap, crateSnap);

        string? preferredCurrency = null;

        if (comp.CurrencyWhitelist.Count > 0)
        {
            foreach (var c in comp.CurrencyWhitelist)
            {
                if (string.IsNullOrWhiteSpace(c))
                    continue;

                if (comp.Listings.Any(l => l.Cost.ContainsKey(c)))
                {
                    preferredCurrency = c;
                    break;
                }
            }

            preferredCurrency ??= comp.CurrencyWhitelist.FirstOrDefault(c => !string.IsNullOrWhiteSpace(c));
        }

        if (string.IsNullOrWhiteSpace(preferredCurrency))
            preferredCurrency = comp.Listings.SelectMany(l => l.Cost.Keys).FirstOrDefault();

        var balance = 0;
        if (!string.IsNullOrWhiteSpace(preferredCurrency))
            userSnap.StackTypeCounts.TryGetValue(preferredCurrency, out balance);

        bool TryPickUiCurrencyAndPrice(StoreListingPrototype listing, out string currencyId, out int price)
        {
            currencyId = string.Empty;
            price = 0;

            if (listing.Cost.Count == 0)
                return false;

            if (listing.Mode == StoreMode.Sell)
            {
                foreach (var cur in comp.CurrencyWhitelist)
                {
                    if (string.IsNullOrWhiteSpace(cur))
                        continue;

                    if (!listing.Cost.TryGetValue(cur, out var pf))
                        continue;

                    var p = (int) MathF.Ceiling(pf);
                    if (p <= 0)
                        continue;

                    currencyId = cur;
                    price = p;
                    return true;
                }

                var first = listing.Cost.First();
                var fp = (int) MathF.Ceiling(first.Value);
                if (fp <= 0 || string.IsNullOrWhiteSpace(first.Key))
                    return false;

                currencyId = first.Key;
                price = fp;
                return true;
            }

            foreach (var cur in comp.CurrencyWhitelist)
            {
                if (string.IsNullOrWhiteSpace(cur))
                    continue;

                if (!listing.Cost.TryGetValue(cur, out var pf))
                    continue;

                var p = (int) MathF.Ceiling(pf);
                if (p <= 0)
                    continue;

                currencyId = cur;
                price = p;
                return true;
            }

            var firstCost = listing.Cost.First();
            var fallbackCur = firstCost.Key;
            var fallbackPrice = (int) MathF.Ceiling(firstCost.Value);

            if (fallbackPrice <= 0 || string.IsNullOrWhiteSpace(fallbackCur))
                return false;

            currencyId = fallbackCur;
            price = fallbackPrice;
            return true;
        }

        var listings = new List<StoreListingData>(comp.Listings.Count + 64);

        foreach (var l in comp.Listings)
        {
            if (string.IsNullOrEmpty(l.ProductEntity))
                continue;

            var cat = l.Categories.Count > 0 ? l.Categories[0] : "Разное";

            var currencyId = string.Empty;
            var price = 0;

            if (TryPickUiCurrencyAndPrice(l, out var cur, out var p))
            {
                currencyId = cur;
                price = p;
            }

            var owned = _logic.GetOwnedFromSnapshot(userSnap, l.ProductEntity, l.MatchMode);

            listings.Add(
                new(
                    l.Id,
                    l.ProductEntity,
                    price,
                    cat,
                    currencyId,
                    l.Mode,
                    owned,
                    l.RemainingCount));
        }

        const string readyCat = "Готово к продаже";

        var readyToSell = listings
            .Where(d => d.Mode == StoreMode.Sell && d.Owned > 0 && d.Remaining != 0)
            .Select(d => new StoreListingData(
                d.Id + "__ready",
                d.ProductEntity,
                d.Price,
                readyCat,
                d.CurrencyId,
                d.Mode,
                d.Owned,
                d.Remaining))
            .ToList();

        if (readyToSell.Count > 0)
            listings.AddRange(readyToSell);

        var crateTotals = new Dictionary<string, int>();
        const string crateCat = "Готово к продаже в ящике";

        if (crateUid is { } crate)
        {
            var plan = _logic.ComputeMassSellPlan(comp, crate);
            crateTotals = plan.IncomeByCurrency;

            foreach (var l in comp.Listings)
            {
                if (l.Mode != StoreMode.Sell || string.IsNullOrEmpty(l.ProductEntity))
                    continue;

                if (l.RemainingCount == 0)
                    continue;

                if (!plan.UnitsByListingId.TryGetValue(l.Id, out var take) || take <= 0)
                    continue;

                if (!plan.PriceByListingId.TryGetValue(l.Id, out var priceData))
                    continue;

                var currencyId = priceData.CurrencyId;
                var price = priceData.UnitPrice;

                if (price <= 0 || string.IsNullOrWhiteSpace(currencyId))
                    continue;

                listings.Add(
                    new(
                        l.Id + "__crate",
                        l.ProductEntity,
                        price,
                        crateCat,
                        currencyId,
                        l.Mode,
                        take,
                        l.RemainingCount));
            }
        }

        var contracts = comp.Contracts.Values
            .Select(c =>
            {
                var targets = new List<ContractTargetClientData>();

                if (c.Targets is { Count: > 0, })
                {
                    foreach (var t in c.Targets)
                    {
                        if (string.IsNullOrWhiteSpace(t.TargetItem) || t.Required <= 0)
                            continue;

                        var td = new ContractTargetClientData(t.TargetItem, t.Required, t.Progress)
                        {
                            MatchMode = t.MatchMode
                        };

                        targets.Add(td);
                    }
                }
                else if (!string.IsNullOrWhiteSpace(c.TargetItem) && c.Required > 0)
                {
                    var td = new ContractTargetClientData(c.TargetItem, c.Required, c.Progress)
                    {
                        MatchMode = c.MatchMode
                    };

                    targets.Add(td);
                }

                var client = new ContractClientData(
                    c.Id,
                    c.Name,
                    c.TargetItem,
                    c.Required,
                    c.Progress,
                    c.Reward,
                    c.RewardCurrency,
                    c.RewardItem,
                    c.RewardItemCount,
                    c.Difficulty,
                    c.Completed,
                    c.Description,
                    targets);

                if (c.RewardCurrencies is { Count: > 0, })
                    client.RewardCurrencies = new(c.RewardCurrencies);

                if (c.RewardItems is { Count: > 0, })
                    client.RewardItems = new(c.RewardItems);

                return client;
            })
            .ToList();

        comp.UiRevision = unchecked(comp.UiRevision + 1);
        var balancesByCurrency = new Dictionary<string, int>(userSnap.StackTypeCounts);
        _ui.SetUiState(
            uid,
            StoreUiKey.Key,
            new StoreUiState(comp.UiRevision, balance, balancesByCurrency, listings, crateTotals, contracts));
    }


    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + TimeSpan.FromSeconds(CheckInterval);

        if (_openStoreUids.Count == 0)
            return;

        foreach (var uid in _openStoreUids.ToArray())
        {
            if (!TryComp(uid, out NcStoreComponent? store) ||
                !TryComp(uid, out TransformComponent? xform))
            {
                _openStoreUids.Remove(uid);
                UnregisterStoreWatch(uid);
                continue;
            }

            if (store.CurrentUser is not { } userUid)
            {
                _openStoreUids.Remove(uid);
                UnregisterStoreWatch(uid);
                continue;
            }

            if (!TryComp(userUid, out TransformComponent? userXform))
            {
                store.CurrentUser = null;
                _openStoreUids.Remove(uid);
                UnregisterStoreWatch(uid);
                continue;
            }

            if (!_xform.InRange(xform.Coordinates, userXform.Coordinates, AutoCloseDistance))
            {
                _ui.CloseUi(uid, StoreUiKey.Key, userUid);
                store.CurrentUser = null;
                _openStoreUids.Remove(uid);
                UnregisterStoreWatch(uid);
                continue;
            }

            if (!_storeSystem.CanUseStore(uid, store, userUid))
            {
                _ui.CloseUi(uid, StoreUiKey.Key, userUid);
                store.CurrentUser = null;
                _openStoreUids.Remove(uid);
                UnregisterStoreWatch(uid);
                _popups.PopupEntity(Loc.GetString("ncstore-no-access"), uid, userUid);
                continue;
            }

            if (EnsureCrateWatchUpToDate(uid, userUid))
                UpdateUiState(uid, store, userUid);
        }
    }


    private void OnAccessReaderChanged(
        EntityUid uid,
        AccessReaderComponent comp,
        ref AccessReaderConfigurationChangedEvent args
    )
    {
        if (TryComp<NcStoreComponent>(uid, out var store) && store.CurrentUser is { } user)
        {
            if (!_storeSystem.CanUseStore(uid, store, user))
            {
                _ui.CloseUi(uid, StoreUiKey.Key, user);
                store.CurrentUser = null;
                _openStoreUids.Remove(uid);
                UnregisterStoreWatch(uid);
            }
        }
    }

    private bool TryGetPulledClosedCrate(EntityUid user, out EntityUid crate)
    {
        crate = default;

        if (!TryComp(user, out PullerComponent? puller))
            return false;

        if (puller.Pulling is not { } pulled)
            return false;

        if (!TryComp(pulled, out EntityStorageComponent? storage) || storage.Open)
            return false;

        crate = pulled;
        return true;
    }


    private void AddWatchedRoot(EntityUid root, EntityUid storeUid)
    {
        if (!_storesByWatchedRoot.TryGetValue(root, out var set))
        {
            set = new();
            _storesByWatchedRoot[root] = set;
        }

        set.Add(storeUid);
    }

    private void RemoveWatchedRoot(EntityUid root, EntityUid storeUid)
    {
        if (!_storesByWatchedRoot.TryGetValue(root, out var set))
            return;

        set.Remove(storeUid);
        if (set.Count == 0)
            _storesByWatchedRoot.Remove(root);
    }

    private void UpdateStoreWatch(EntityUid storeUid, EntityUid user, EntityUid? crate)
    {
        if (user == EntityUid.Invalid)
        {
            UnregisterStoreWatch(storeUid);
            return;
        }

        if (_watchByStore.TryGetValue(storeUid, out var prev))
        {
            if (prev.User == user && prev.Crate == crate)
                return;

            if (prev.User != EntityUid.Invalid)
                RemoveWatchedRoot(prev.User, storeUid);

            if (prev.Crate is { } oldCrate)
                RemoveWatchedRoot(oldCrate, storeUid);
        }

        _watchByStore[storeUid] = (user, crate);

        AddWatchedRoot(user, storeUid);
        if (crate is { } c)
            AddWatchedRoot(c, storeUid);
    }


    private void UnregisterStoreWatch(EntityUid storeUid)
    {
        if (!_watchByStore.TryGetValue(storeUid, out var info))
            return;

        if (info.User != EntityUid.Invalid)
            RemoveWatchedRoot(info.User, storeUid);

        if (info.Crate is { } crate)
            RemoveWatchedRoot(crate, storeUid);

        _watchByStore.Remove(storeUid);
    }


    private void RefreshStoresAffectedBy(EntityUid changedEntity)
    {
        if (_storesByWatchedRoot.Count == 0)
            return;

        var affectedStores = new HashSet<EntityUid>();

        void AddStoresForRoot(EntityUid root)
        {
            if (_storesByWatchedRoot.TryGetValue(root, out var stores))
            {
                foreach (var s in stores)
                    affectedStores.Add(s);
            }
        }

        var cur = changedEntity;

        for (var i = 0; i < 64; i++)
        {
            AddStoresForRoot(cur);

            if (!TryComp(cur, out TransformComponent? xform))
                break;

            var parent = xform.ParentUid;
            if (parent == EntityUid.Invalid || parent == cur)
                break;

            if (TryComp(parent, out ContainerManagerComponent? parentContainers))
            {
                foreach (var container in parentContainers.Containers.Values)
                {
                    if (!container.Contains(cur))
                        continue;

                    cur = parent;
                    goto NextStep;
                }
            }

            cur = parent;

            NextStep: ;
        }

        if (affectedStores.Count == 0)
            return;

        foreach (var storeUid in affectedStores)
        {
            if (!TryComp(storeUid, out NcStoreComponent? store) ||
                store.CurrentUser is not { } user)
            {
                _openStoreUids.Remove(storeUid);
                UnregisterStoreWatch(storeUid);
                continue;
            }

            UpdateUiState(storeUid, store, user);
        }
    }


    private void OnUserEntInserted(EntityUid uid, ContainerManagerComponent comp, EntInsertedIntoContainerMessage args)
    {
        RefreshStoresAffectedBy(args.Entity);
        RefreshStoresAffectedBy(uid);
    }

    private void OnUserEntRemoved(EntityUid uid, ContainerManagerComponent comp, EntRemovedFromContainerMessage args)
    {
        RefreshStoresAffectedBy(args.Entity);
        RefreshStoresAffectedBy(uid);
    }


    private void OnStackCountChanged(
        EntityUid uid,
        StackComponent comp,
        ref StackCountChangedEvent args
    ) =>
        RefreshStoresAffectedBy(uid);


    private void OnClaimContract(EntityUid uid, NcStoreComponent comp, ClaimContractBoundMessage msg)
    {
        if (comp.CurrentUser is not { } user)
            return;

        UpdateContractsProgress(comp, user);

        if (!_contracts.TryClaim(uid, user, msg.ContractId))
            return;

        _popups.PopupEntity("Контракт выполнен!", uid, user);

        UpdateUiState(uid, comp, user);
    }

    private void UpdateContractsProgress(NcStoreComponent comp, EntityUid user)
    {
        _logic.InvalidateInventoryCache(user);

        NcStoreLogicSystem.InventorySnapshot? crateSnap = null;
        if (TryGetPulledClosedCrate(user, out var crate))
        {
            _logic.InvalidateInventoryCache(crate);
            crateSnap = _logic.BuildInventorySnapshot(crate);
        }

        var userSnap = _logic.BuildInventorySnapshot(user);
        UpdateContractsProgress(comp, userSnap, crateSnap);
    }


    private void UpdateContractsProgress(
        NcStoreComponent comp,
        in NcStoreLogicSystem.InventorySnapshot userSnap,
        NcStoreLogicSystem.InventorySnapshot? crateSnap
    )
    {
        if (comp.Contracts.Count == 0)
            return;

        foreach (var (_, contract) in comp.Contracts)
        {
            var targets = contract.Targets;

            if (targets.Count > 0)
            {
                var totalRequired = 0;
                var totalProgress = 0;

                foreach (var t in targets)
                {
                    if (string.IsNullOrWhiteSpace(t.TargetItem) || t.Required <= 0)
                    {
                        t.Progress = 0;
                        continue;
                    }

                    var owned = _logic.GetOwnedFromSnapshot(userSnap, t.TargetItem, t.MatchMode);

                    if (crateSnap.HasValue)
                        owned += _logic.GetOwnedFromSnapshot(crateSnap.Value, t.TargetItem, t.MatchMode);

                    var prog = Math.Min(owned, t.Required);
                    t.Progress = prog;

                    totalRequired += t.Required;
                    totalProgress += prog;
                }

                contract.Required = totalRequired;
                contract.Progress = totalProgress;

                if (targets.Count > 0)
                    contract.TargetItem = targets[0].TargetItem;
            }
            else
            {
                if (string.IsNullOrWhiteSpace(contract.TargetItem) || contract.Required <= 0)
                {
                    contract.Progress = 0;
                    continue;
                }

                var owned = _logic.GetOwnedFromSnapshot(userSnap, contract.TargetItem, contract.MatchMode);

                if (crateSnap.HasValue)
                    owned += _logic.GetOwnedFromSnapshot(crateSnap.Value, contract.TargetItem, contract.MatchMode);

                contract.Progress = Math.Min(owned, contract.Required);
            }
        }
    }
}
