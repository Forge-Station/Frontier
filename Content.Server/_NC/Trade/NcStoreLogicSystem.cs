using System.Linq;
using Content.Shared._NC.Trade;
using Content.Shared.Clothing.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;


namespace Content.Server._NC.Trade;


public sealed class NcStoreLogicSystem : EntitySystem
{
    private static readonly ISawmill Sawmill = Logger.GetSawmill("ncstore-logic");

    [Dependency] private readonly IComponentFactory _compFactory = default!;
    [Dependency] private readonly IEntityManager _ents = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    private readonly Dictionary<EntityUid, List<EntityUid>> _inventoryCache = new();

    private readonly Dictionary<string, string?> _productStackTypeCache = new();

    private readonly Dictionary<string, string[]> _protoAndAncestorsCache = new();

    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly SharedStackSystem _stacks = default!;

    public void InvalidateInventoryCache(EntityUid root) => _inventoryCache.Remove(root);


    private PrototypeMatchMode ResolveMatchMode(string expectedProtoId, PrototypeMatchMode configured)
    {
        // Explicit override always wins.
        if (configured == PrototypeMatchMode.Descendants)
            return PrototypeMatchMode.Descendants;

        // Auto: abstract prototypes act as "category" keys and match all descendants.
        if (_protos.TryIndex<EntityPrototype>(expectedProtoId, out var expectedProto) && expectedProto.Abstract)
            return PrototypeMatchMode.Descendants;

        return PrototypeMatchMode.Exact;
    }


    public int GetBalance(EntityUid user, string stackType)
    {
        var total = 0;
        foreach (var entity in EnumerateDeepItemsUnique(user))
            if (_ents.TryGetComponent(entity, out StackComponent? stack) &&
                stack.StackTypeId == stackType)
                total += stack.Count;

        return total;
    }

    public InventorySnapshot BuildInventorySnapshot(EntityUid root)
    {
        var protoCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var ancestorCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var stackTypeCounts = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var ent in EnumerateDeepItemsUnique(root))
        {
            if (IsProtectedFromDirectSale(root, ent))
                continue;

            if (_ents.TryGetComponent(ent, out StackComponent? stack))
            {
                var cnt = Math.Max(stack.Count, 0);
                if (cnt > 0 && !string.IsNullOrWhiteSpace(stack.StackTypeId))
                {
                    stackTypeCounts.TryGetValue(stack.StackTypeId, out var prev);
                    stackTypeCounts[stack.StackTypeId] = prev + cnt;
                }

                continue;
            }

            if (!_ents.TryGetComponent(ent, out MetaDataComponent? meta) || meta.EntityPrototype is null)
                continue;

            var proto = meta.EntityPrototype;
            if (!protoCounts.TryAdd(proto.ID, 1))
                protoCounts[proto.ID] += 1;

            foreach (var id in GetProtoAndAncestors(proto))
            {
                ancestorCounts.TryGetValue(id, out var prev);
                ancestorCounts[id] = prev + 1;
            }
        }

        return new(protoCounts, ancestorCounts, stackTypeCounts);
    }


    public int GetOwnedFromSnapshot(in InventorySnapshot snapshot, string productProtoId) =>
        GetOwnedFromSnapshot(snapshot, productProtoId, PrototypeMatchMode.Exact);

    public int GetOwnedFromSnapshot(in InventorySnapshot snapshot, string productProtoId, PrototypeMatchMode matchMode)
    {
        var stackType = GetProductStackType(productProtoId);
        if (stackType != null)
            return snapshot.StackTypeCounts.TryGetValue(stackType, out var cnt) ? cnt : 0;

        var effective = ResolveMatchMode(productProtoId, matchMode);

        if (effective == PrototypeMatchMode.Descendants)
            return snapshot.AncestorCounts.TryGetValue(productProtoId, out var units) ? units : 0;

        return snapshot.ProtoCounts.TryGetValue(productProtoId, out var exact) ? exact : 0;
    }

    private string? GetProductStackType(string productProtoId)
    {
        if (_productStackTypeCache.TryGetValue(productProtoId, out var cached))
            return cached;

        string? stackType = null;

        if (_protos.TryIndex<EntityPrototype>(productProtoId, out var expectedProto))
        {
            var stackName = _compFactory.GetComponentName(typeof(StackComponent));
            if (expectedProto.TryGetComponent(stackName, out StackComponent? prodStackDef))
                stackType = prodStackDef.StackTypeId;
        }

        _productStackTypeCache[productProtoId] = stackType;
        return stackType;
    }

    private string[] GetProtoAndAncestors(EntityPrototype proto)
    {
        var id = proto.ID;
        if (_protoAndAncestorsCache.TryGetValue(id, out var cached))
            return cached;

        var visited = new HashSet<string>();
        var result = new List<string>();
        var stack = new Stack<string>();
        stack.Push(id);

        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            if (!visited.Add(cur))
                continue;

            result.Add(cur);

            if (!_protos.TryIndex<EntityPrototype>(cur, out var curProto) || curProto.Parents == null)
                continue;

            foreach (var p in curProto.Parents)
                if (!string.IsNullOrWhiteSpace(p))
                    stack.Push(p);
        }

        var arr = result.ToArray();
        _protoAndAncestorsCache[id] = arr;
        return arr;
    }

    private bool TryPickCurrencyForBuy(
        NcStoreComponent store,
        StoreListingPrototype listing,
        EntityUid user,
        out string currency,
        out int price
    )
    {
        currency = string.Empty;
        price = 0;

        if (listing.Cost.Count == 0)
            return false;

        var balances = new Dictionary<string, int>(store.CurrencyWhitelist.Count);
        foreach (var cur in store.CurrencyWhitelist)
        {
            if (string.IsNullOrWhiteSpace(cur))
                continue;

            balances[cur] = GetBalance(user, cur);
        }

        foreach (var cur in store.CurrencyWhitelist)
        {
            if (string.IsNullOrWhiteSpace(cur))
                continue;

            if (!listing.Cost.TryGetValue(cur, out var priceF))
                continue;

            var p = (int) MathF.Ceiling(priceF);
            if (p <= 0)
                continue;

            if (!balances.TryGetValue(cur, out var bal) || bal < p)
                continue;

            currency = cur;
            price = p;
            return true;
        }

        var firstCost = listing.Cost.First();
        var fallbackCur = firstCost.Key;
        var fallbackPrice = (int) MathF.Ceiling(firstCost.Value);
        if (fallbackPrice <= 0)
            return false;

        var fallbackBalance = GetBalance(user, fallbackCur);
        if (fallbackBalance < fallbackPrice)
            return false;

        currency = fallbackCur;
        price = fallbackPrice;
        return true;
    }

    private bool IsProtoOrDescendant(EntityPrototype candidate, string expectedId)
    {
        if (candidate.ID == expectedId)
            return true;

        var visited = new HashSet<string>();
        var stack = new Stack<string>();

        if (candidate.Parents != null)
        {
            foreach (var p in candidate.Parents)
                stack.Push(p);
        }

        while (stack.Count > 0)
        {
            var pid = stack.Pop();
            if (!visited.Add(pid))
                continue;

            if (pid == expectedId)
                return true;

            if (_protos.TryIndex<EntityPrototype>(pid, out var parentProto))
            {
                if (parentProto.Parents != null)
                {
                    foreach (var gp in parentProto.Parents)
                        stack.Push(gp);
                }
            }
        }

        return false;
    }


    private bool TryPickCurrencyForSell(
        NcStoreComponent store,
        StoreListingPrototype listing,
        out string currency,
        out int price
    )
    {
        currency = string.Empty;
        price = 0;

        if (listing.Cost.Count == 0)
            return false;

        foreach (var cur in store.CurrencyWhitelist)
        {
            if (string.IsNullOrWhiteSpace(cur))
                continue;

            if (!listing.Cost.TryGetValue(cur, out var priceF))
                continue;

            var p = (int) MathF.Ceiling(priceF);
            if (p <= 0)
                continue;

            currency = cur;
            price = p;
            return true;
        }

        var firstCost = listing.Cost.First();
        var fallbackCur = firstCost.Key;
        var fallbackPrice = (int) MathF.Ceiling(firstCost.Value);
        if (fallbackPrice <= 0)
            return false;

        currency = fallbackCur;
        price = fallbackPrice;
        return true;
    }

    public bool TryBuy(string listingId, EntityUid machine, NcStoreComponent? store, EntityUid user, int count = 1)
    {
        if (store == null || store.Listings.Count == 0 || count <= 0)
            return false;

        var listing = store.Listings.FirstOrDefault(x => x.Id == listingId && x.Mode == StoreMode.Buy);
        if (listing == null)
            return false;

        if (!_protos.TryIndex<EntityPrototype>(listing.ProductEntity, out _))
            return false;

        if (!TryPickCurrencyForBuy(store, listing, user, out var currency, out var unitPrice))
            return false;

        var maxByRemaining = listing.RemainingCount >= 0 ? listing.RemainingCount : int.MaxValue;

        InvalidateInventoryCache(user);
        var balance = GetBalance(user, currency);
        var maxByMoney = unitPrice > 0 ? balance / unitPrice : int.MaxValue;

        var maxPossible = Math.Min(maxByRemaining, maxByMoney);
        if (maxPossible <= 0)
            return false;

        var actual = Math.Min(count, maxPossible);

        var totalPriceL = (long) unitPrice * actual;
        if (totalPriceL > int.MaxValue)
            return false;

        var totalPrice = (int) totalPriceL;
        if (!TryTakeCurrency(user, currency, totalPrice))
            return false;

        var spawned = 0;
        for (var i = 0; i < actual; i++)
            if (TrySpawnProduct(listing.ProductEntity, user))
                spawned++;
            else
                GiveCurrency(user, currency, unitPrice);

        if (spawned <= 0)
            return false;

        if (listing.RemainingCount > 0)
            listing.RemainingCount = Math.Max(0, listing.RemainingCount - spawned);

        Sawmill.Info($"TryBuy: OK {listing.ProductEntity} x{spawned} for {unitPrice} {currency} each");
        return true;
    }

    public bool TrySell(string listingId, EntityUid machine, NcStoreComponent? store, EntityUid user, int count = 1)
    {
        if (store == null || store.Listings.Count == 0 || count <= 0)
            return false;

        var listing = store.Listings.FirstOrDefault(x => x.Id == listingId && x.Mode == StoreMode.Sell);
        if (listing == null)
            return false;

        if (!TryPickCurrencyForSell(store, listing, out var currency, out var unitPrice) || unitPrice <= 0)
            return false;

        InvalidateInventoryCache(user);
        var owned = GetOwned(user, listing.ProductEntity, listing.MatchMode);
        var maxByRemaining = listing.RemainingCount >= 0 ? listing.RemainingCount : int.MaxValue;

        var maxPossible = Math.Min(owned, maxByRemaining);
        if (maxPossible <= 0)
            return false;

        var actual = Math.Min(count, maxPossible);

        if (!TryTakeProductUnits(user, listing.ProductEntity, actual, listing.MatchMode))
            return false;

        var totalL = (long) unitPrice * actual;
        if (totalL > int.MaxValue)
            return false;

        GiveCurrency(user, currency, (int) totalL);

        if (listing.RemainingCount > 0)
            listing.RemainingCount = Math.Max(0, listing.RemainingCount - actual);

        Sawmill.Info($"TrySell: OK {listing.ProductEntity} x{actual} for {unitPrice} {currency} each");
        return true;
    }

    public bool TrySellFromContainer(
        string listingId,
        EntityUid machine,
        NcStoreComponent? store,
        EntityUid user,
        EntityUid container,
        int count = 1
    )
    {
        if (store == null || store.Listings.Count == 0 || count <= 0)
            return false;

        var listing = store.Listings.FirstOrDefault(x => x.Id == listingId && x.Mode == StoreMode.Sell);
        if (listing == null)
            return false;

        if (!TryPickCurrencyForSell(store, listing, out var currency, out var unitPrice) || unitPrice <= 0)
            return false;

        InvalidateInventoryCache(container);

        var owned = GetOwnedInRoot(container, listing.ProductEntity, listing.MatchMode);
        var maxByRemaining = listing.RemainingCount >= 0 ? listing.RemainingCount : int.MaxValue;

        var maxPossible = Math.Min(owned, maxByRemaining);
        if (maxPossible <= 0)
            return false;

        var actual = Math.Min(count, maxPossible);

        if (!TryTakeProductUnitsFromRoot(container, listing.ProductEntity, actual, listing.MatchMode))
            return false;

        var totalL = (long) unitPrice * actual;
        if (totalL > int.MaxValue)
            return false;

        GiveCurrency(user, currency, (int) totalL);

        if (listing.RemainingCount > 0)
            listing.RemainingCount = Math.Max(0, listing.RemainingCount - actual);

        Sawmill.Info(
            $"TrySellFromContainer: OK {listing.ProductEntity} x{actual} for {unitPrice} {currency} each (container={ToPrettyString(container)})");
        return true;
    }


    private int GetOwnedInternal(EntityUid root, string productProtoId, PrototypeMatchMode matchMode)
    {
        var total = 0;

        var expectedStackType = GetProductStackType(productProtoId);
        var effective = ResolveMatchMode(productProtoId, matchMode);

        foreach (var ent in EnumerateDeepItemsUnique(root))
        {
            if (IsProtectedFromDirectSale(root, ent))
                continue;

            if (expectedStackType != null &&
                _ents.TryGetComponent(ent, out StackComponent? stack) &&
                stack.StackTypeId == expectedStackType)
            {
                total += Math.Max(stack.Count, 0);
                continue;
            }

            if (!_ents.TryGetComponent(ent, out MetaDataComponent? meta) || meta.EntityPrototype is null)
                continue;

            if (effective == PrototypeMatchMode.Descendants)
            {
                if (IsProtoOrDescendant(meta.EntityPrototype, productProtoId))
                    total += 1;
            }
            else
            {
                if (meta.EntityPrototype.ID == productProtoId)
                    total += 1;
            }
        }

        return total;
    }


    public int GetOwned(EntityUid user, string productProtoId) =>
        GetOwnedInternal(user, productProtoId, PrototypeMatchMode.Exact);

    public int GetOwned(EntityUid user, string productProtoId, PrototypeMatchMode matchMode) =>
        GetOwnedInternal(user, productProtoId, matchMode);

    public int GetOwnedInRoot(EntityUid root, string productProtoId) =>
        GetOwnedInternal(root, productProtoId, PrototypeMatchMode.Exact);

    public int GetOwnedInRoot(EntityUid root, string productProtoId, PrototypeMatchMode matchMode) =>
        GetOwnedInternal(root, productProtoId, matchMode);

    private bool TryTakeProductUnitsInternal(EntityUid root, string protoId, int amount, PrototypeMatchMode matchMode)
    {
        if (amount <= 0)
            return true;

        InvalidateInventoryCache(root);

        var stackType = GetProductStackType(protoId);

        if (stackType == null)
        {
            var left = amount;

            var effective = ResolveMatchMode(protoId, matchMode);

            if (effective == PrototypeMatchMode.Exact)
            {
                foreach (var ent in EnumerateDeepItemsUnique(root))
                {
                    if (left <= 0)
                        break;

                    if (IsProtectedFromDirectSale(root, ent))
                        continue;

                    if (!_ents.TryGetComponent(ent, out MetaDataComponent? meta) || meta.EntityPrototype is null)
                        continue;

                    if (meta.EntityPrototype.ID != protoId)
                        continue;

                    if (_ents.EntityExists(ent))
                    {
                        _ents.DeleteEntity(ent);
                        left -= 1;
                    }
                }

                return left <= 0;
            }

            foreach (var ent in EnumerateDeepItemsUnique(root))
            {
                if (left <= 0)
                    break;

                if (IsProtectedFromDirectSale(root, ent))
                    continue;

                if (!_ents.TryGetComponent(ent, out MetaDataComponent? meta) || meta.EntityPrototype is null)
                    continue;

                if (meta.EntityPrototype.ID != protoId)
                    continue;

                if (_ents.EntityExists(ent))
                {
                    _ents.DeleteEntity(ent);
                    left -= 1;
                }
            }

            foreach (var ent in EnumerateDeepItemsUnique(root))
            {
                if (left <= 0)
                    break;

                if (IsProtectedFromDirectSale(root, ent))
                    continue;

                if (!_ents.TryGetComponent(ent, out MetaDataComponent? meta) || meta.EntityPrototype is null)
                    continue;

                if (meta.EntityPrototype.ID == protoId)
                    continue;

                if (!IsProtoOrDescendant(meta.EntityPrototype, protoId))
                    continue;

                if (_ents.EntityExists(ent))
                {
                    _ents.DeleteEntity(ent);
                    left -= 1;
                }
            }

            return left <= 0;
        }

        var leftStack = amount;
        foreach (var ent in EnumerateDeepItemsUnique(root))
        {
            if (leftStack <= 0)
                break;

            if (IsProtectedFromDirectSale(root, ent))
                continue;

            if (!_ents.TryGetComponent(ent, out StackComponent? stack) || stack.StackTypeId != stackType)
                continue;

            var have = Math.Max(stack.Count, 0);
            if (have <= 0)
                continue;

            var take = Math.Min(have, leftStack);
            var newCount = have - take;
            _stacks.SetCount(ent, newCount, stack);
            if (newCount <= 0 && _ents.EntityExists(ent))
                _ents.DeleteEntity(ent);

            leftStack -= take;
        }

        return leftStack <= 0;
    }


    public bool TryTakeProductUnits(EntityUid user, string protoId, int amount) =>
        TryTakeProductUnitsInternal(user, protoId, amount, PrototypeMatchMode.Exact);

    public bool TryTakeProductUnits(EntityUid user, string protoId, int amount, PrototypeMatchMode matchMode) =>
        TryTakeProductUnitsInternal(user, protoId, amount, matchMode);

    public bool TryTakeProductUnitsFromRoot(EntityUid root, string protoId, int amount) =>
        TryTakeProductUnitsInternal(root, protoId, amount, PrototypeMatchMode.Exact);

    public bool TryTakeProductUnitsFromRoot(EntityUid root, string protoId, int amount, PrototypeMatchMode matchMode) =>
        TryTakeProductUnitsInternal(root, protoId, amount, matchMode);

    public bool TryExchange(
        string listingId,
        EntityUid machine,
        NcStoreComponent? store,
        EntityUid user
    )
    {
        if (store == null || store.Listings.Count == 0)
            return false;

        var listing = store.Listings.FirstOrDefault(x => x.Id == listingId && x.Mode == StoreMode.Exchange);

        if (listing == null)
            return false;

        if (string.IsNullOrEmpty(listing.ProductEntity))
            return false;

        var requiredCount = listing.RemainingCount > 0
            ? listing.RemainingCount
            : 1;

        if (requiredCount <= 0)
            return false;

        InvalidateInventoryCache(user);
        var owned = GetOwned(user, listing.ProductEntity, listing.MatchMode);
        if (owned < requiredCount)
            return false;

        if (!TryPickCurrencyForSell(store, listing, out var currencyId, out var rewardPerUnit) ||
            rewardPerUnit <= 0)
            return false;

        if (!TryTakeProductUnits(user, listing.ProductEntity, requiredCount, listing.MatchMode))
            return false;

        var totalRewardL = (long) rewardPerUnit * requiredCount;
        if (totalRewardL > int.MaxValue)
            return false;

        GiveCurrency(user, currencyId, (int) totalRewardL);

        listing.RemainingCount = 0;

        return true;
    }


    private IEnumerable<EntityUid> EnumerateDeepItemsUnique(EntityUid owner)
    {
        if (_inventoryCache.TryGetValue(owner, out var cached))
        {
            foreach (var ent in cached)
                if (_ents.EntityExists(ent))
                    yield return ent;

            yield break;
        }

        var visited = new HashSet<EntityUid>();
        var queue = new Queue<EntityUid>();
        var result = new List<EntityUid>();

        void Enqueue(EntityUid uid)
        {
            if (!visited.Add(uid))
                return;

            queue.Enqueue(uid);
            result.Add(uid);
        }


        if (_ents.TryGetComponent(owner, out InventoryComponent? inventory))
        {
            var slotEnum = new InventorySystem.InventorySlotEnumerator(inventory);
            while (slotEnum.NextItem(out var item))
                Enqueue(item);
        }

        if (_ents.TryGetComponent(owner, out ItemSlotsComponent? itemSlots))
        {
            foreach (var slot in itemSlots.Slots.Values)
                if (slot.HasItem && slot.Item.HasValue)
                    Enqueue(slot.Item.Value);
        }

        if (_ents.TryGetComponent(owner, out HandsComponent? hands))
        {
            foreach (var handId in hands.Hands.Keys)
            {
                if (_hands.TryGetHeldItem((owner, hands), handId, out var heldEntity) && heldEntity != null)
                    Enqueue(heldEntity.Value);
            }
        }

        if (_ents.TryGetComponent(owner, out ContainerManagerComponent? cmcRoot))
        {
            foreach (var container in cmcRoot.Containers.Values)
            {
                foreach (var entity in container.ContainedEntities)
                    Enqueue(entity);
            }
        }

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();

            if (_ents.TryGetComponent(current, out ContainerManagerComponent? cmc))
            {
                foreach (var container in cmc.Containers.Values)
                {
                    foreach (var child in container.ContainedEntities)
                        Enqueue(child);
                }
            }
        }

        _inventoryCache[owner] = result;

        foreach (var ent in result)
            if (_ents.EntityExists(ent))
                yield return ent;
    }

    private bool TryTakeCurrency(EntityUid user, string stackType, int amount)
    {
        if (amount <= 0)
            return true;

        InvalidateInventoryCache(user);

        var cands = new List<(EntityUid Ent, int Count)>();
        var total = 0;

        foreach (var ent in EnumerateDeepItemsUnique(user))
            if (_ents.TryGetComponent(ent, out StackComponent? st) &&
                st.StackTypeId == stackType)
            {
                var cnt = Math.Max(st.Count, 0);
                if (cnt <= 0)
                    continue;

                cands.Add((ent, cnt));
                total += cnt;
            }

        if (total < amount)
            return false;

        cands.Sort((a, b) => a.Count.CompareTo(b.Count));

        var left = amount;
        foreach (var (ent, have) in cands)
        {
            if (left <= 0)
                break;

            var take = Math.Min(have, left);
            if (_ents.TryGetComponent(ent, out StackComponent? st))
            {
                var newCount = st.Count - take;
                _stacks.SetCount(ent, newCount, st);
                if (newCount <= 0 && _ents.EntityExists(ent))
                    _ents.DeleteEntity(ent);
            }

            left -= take;
        }

        return left <= 0;
    }

    public void GiveCurrency(EntityUid user, string stackType, int amount)
    {
        if (amount <= 0)
            return;

        if (string.IsNullOrWhiteSpace(stackType))
            return;

        InvalidateInventoryCache(user);

        if (!_protos.TryIndex<StackPrototype>(stackType, out var proto))
            return;

        long remaining = amount;

        foreach (var ent in EnumerateDeepItemsUnique(user))
        {
            if (remaining <= 0)
                break;

            if (!_ents.TryGetComponent(ent, out StackComponent? st) || st.StackTypeId != stackType)
                continue;

            var maxPerStack = proto.MaxCount ?? int.MaxValue;
            if (maxPerStack <= 0)
                maxPerStack = 1;

            var canAdd = (long) maxPerStack - st.Count;
            if (canAdd <= 0)
                continue;

            var add = Math.Min(canAdd, remaining);

            var newCountL = st.Count + add;
            var newCount = (int) Math.Clamp(newCountL, 0L, maxPerStack);

            _stacks.SetCount(ent, newCount, st);
            remaining -= add;
        }

        if (remaining <= 0)
            return;

        var coords = _ents.GetComponent<TransformComponent>(user).Coordinates;

        var perStackLimit = proto.MaxCount ?? int.MaxValue;
        if (perStackLimit <= 0)
            perStackLimit = 1;

        while (remaining > 0)
        {
            var addL = Math.Min(remaining, perStackLimit);
            var add = (int) Math.Clamp(addL, 1L, perStackLimit);

            var spawned = _ents.SpawnEntity(proto.Spawn, coords);

            if (_ents.TryGetComponent(spawned, out StackComponent? newStack))
                _stacks.SetCount(spawned, add, newStack);

            if (_ents.HasComponent<HandsComponent>(user))
                _hands.TryPickupAnyHand(user, spawned, false);

            remaining -= add;
        }
    }

    private int GetInheritanceDepth(string protoId)
    {
        if (!_protos.TryIndex<EntityPrototype>(protoId, out var proto))
            return 0;

        var visited = new HashSet<string>();
        var maxDepth = 0;
        var stack = new Stack<(string Id, int Depth)>();

        if (proto.Parents != null)
        {
            foreach (var pid in proto.Parents)
                if (visited.Add(pid))
                    stack.Push((pid, 1));
        }

        while (stack.Count > 0)
        {
            var (currentId, depth) = stack.Pop();
            if (depth > maxDepth)
                maxDepth = depth;

            if (_protos.TryIndex<EntityPrototype>(currentId, out var parentProto) && parentProto.Parents != null)
            {
                foreach (var pid in parentProto.Parents)
                    if (visited.Add(pid))
                        stack.Push((pid, depth + 1));
            }
        }

        return maxDepth;
    }

    private static void SafeAddIncome(Dictionary<string, int> income, string currencyId, long delta)
    {
        if (delta <= 0)
            return;

        if (!income.TryGetValue(currencyId, out var cur))
            cur = 0;

        var sum = cur + delta;
        if (sum >= int.MaxValue)
            income[currencyId] = int.MaxValue;
        else
            income[currencyId] = (int) sum;
    }

    public MassSellPlan ComputeMassSellPlan(NcStoreComponent store, EntityUid container)
    {
        var incomeByCurrency = new Dictionary<string, int>(StringComparer.Ordinal);
        var unitsByListingId = new Dictionary<string, int>(StringComparer.Ordinal);
        var priceByListingId = new Dictionary<string, (string, int)>(StringComparer.Ordinal);
        var steps = new List<MassSellStep>();

        if (store.Listings.Count == 0)
            return new(incomeByCurrency, unitsByListingId, priceByListingId, steps);

        InvalidateInventoryCache(container);

        var stackTypeCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var protoCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        var protoCache = new Dictionary<string, EntityPrototype>(StringComparer.Ordinal);

        foreach (var ent in EnumerateDeepItemsUnique(container))
        {
            if (_ents.TryGetComponent(ent, out StackComponent? st))
            {
                var cnt = Math.Max(st.Count, 0);
                if (cnt > 0 && !string.IsNullOrWhiteSpace(st.StackTypeId))
                {
                    stackTypeCounts.TryGetValue(st.StackTypeId, out var prev);
                    stackTypeCounts[st.StackTypeId] = prev + cnt;
                }

                continue;
            }

            if (!_ents.TryGetComponent(ent, out MetaDataComponent? meta) || meta.EntityPrototype is null)
                continue;

            var proto = meta.EntityPrototype;

            if (!protoCounts.TryAdd(proto.ID, 1))
                protoCounts[proto.ID] += 1;

            protoCache[proto.ID] = proto;
        }

        if (stackTypeCounts.Count == 0 && protoCounts.Count == 0)
            return new(incomeByCurrency, unitsByListingId, priceByListingId, steps);

        var protoIds = protoCounts.Count > 0
            ? protoCounts.Keys
                .OrderByDescending(GetInheritanceDepth)
                .ThenBy(x => x, StringComparer.Ordinal)
                .ToArray()
            : Array.Empty<string>();

        var sellListings = store.Listings
            .Where(l =>
                l.Mode == StoreMode.Sell &&
                !string.IsNullOrEmpty(l.ProductEntity) &&
                l.RemainingCount != 0)
            .OrderByDescending(l => GetInheritanceDepth(l.ProductEntity))
            .ThenBy(l => l.ProductEntity, StringComparer.Ordinal)
            .ThenBy(l => l.Id, StringComparer.Ordinal)
            .ToArray();

        if (sellListings.Length == 0)
            return new(incomeByCurrency, unitsByListingId, priceByListingId, steps);

        var stackName = _compFactory.GetComponentName(typeof(StackComponent));

        foreach (var listing in sellListings)
        {
            if (!TryPickCurrencyForSell(store, listing, out var currencyId, out var unitPrice))
                continue;

            if (unitPrice <= 0 || string.IsNullOrWhiteSpace(currencyId))
                continue;

            var remaining = listing.RemainingCount;
            if (remaining < -1)
                remaining = -1;

            var maxByRemaining = remaining >= 0 ? remaining : int.MaxValue;
            if (maxByRemaining <= 0)
                continue;

            var maxTakeByInt = int.MaxValue / unitPrice;
            if (maxTakeByInt <= 0)
                continue;

            var want = Math.Min(maxByRemaining, maxTakeByInt);

            string? expectedStackType = null;
            if (_protos.TryIndex<EntityPrototype>(listing.ProductEntity, out var prodProto) &&
                prodProto.TryGetComponent(stackName, out StackComponent? prodStackDef))
                expectedStackType = prodStackDef.StackTypeId;

            var taken = 0;

            if (!string.IsNullOrEmpty(expectedStackType))
            {
                if (!stackTypeCounts.TryGetValue(expectedStackType, out var available) || available <= 0)
                    continue;

                taken = Math.Min(available, want);
                stackTypeCounts[expectedStackType] = available - taken;
            }
            else
            {
                if (protoIds.Length == 0)
                    continue;

                if (listing.MatchMode != PrototypeMatchMode.Descendants)
                {
                    if (!protoCounts.TryGetValue(listing.ProductEntity, out var available) || available <= 0)
                        continue;

                    taken = Math.Min(available, want);
                    protoCounts[listing.ProductEntity] = available - taken;
                }
                else
                {
                    foreach (var protoId in protoIds)
                    {
                        if (taken >= want)
                            break;

                        if (!protoCounts.TryGetValue(protoId, out var available) || available <= 0)
                            continue;

                        if (!protoCache.TryGetValue(protoId, out var proto) &&
                            !_protos.TryIndex(protoId, out proto))
                            continue;

                        protoCache[protoId] = proto;

                        if (!IsProtoOrDescendant(proto, listing.ProductEntity))
                            continue;

                        var take = Math.Min(available, want - taken);
                        if (take <= 0)
                            continue;

                        protoCounts[protoId] = available - take;
                        taken += take;
                    }
                }
            }

            if (taken <= 0)
                continue;

            var total = (long) unitPrice * taken;
            SafeAddIncome(incomeByCurrency, currencyId, total);

            unitsByListingId[listing.Id] = taken;
            priceByListingId[listing.Id] = (currencyId, unitPrice);
            steps.Add(new(listing, currencyId, unitPrice, taken));
        }

        return new(incomeByCurrency, unitsByListingId, priceByListingId, steps);
    }

    public bool TryMassSellFromContainer(
        EntityUid machine,
        NcStoreComponent store,
        EntityUid user,
        EntityUid container
    )
    {
        if (store.Listings.Count == 0)
            return false;

        var plan = ComputeMassSellPlan(store, container);
        if (plan.Steps.Count == 0 || plan.IncomeByCurrency.Count == 0)
            return false;

        var incomeActual = new Dictionary<string, int>(StringComparer.Ordinal);
        var any = false;

        foreach (var step in plan.Steps)
        {
            if (step.Count <= 0 ||
                step.UnitPrice <= 0 ||
                string.IsNullOrWhiteSpace(step.CurrencyId) ||
                string.IsNullOrWhiteSpace(step.Listing.ProductEntity))
                continue;

            var listing = step.Listing;

            var remaining = listing.RemainingCount;
            if (remaining < -1)
                remaining = -1;

            var maxByRemaining = remaining >= 0 ? remaining : int.MaxValue;
            if (maxByRemaining <= 0)
                continue;

            var take = Math.Min(step.Count, maxByRemaining);
            if (take <= 0)
                continue;

            if (!TryTakeProductUnitsInternal(container, listing.ProductEntity, take, listing.MatchMode))
                continue;

            if (listing.RemainingCount > 0)
                listing.RemainingCount = Math.Max(0, listing.RemainingCount - take);

            var total = (long) step.UnitPrice * take;
            SafeAddIncome(incomeActual, step.CurrencyId, total);

            any = true;
        }

        if (!any || incomeActual.Count == 0)
            return false;

        foreach (var (currency, amount) in incomeActual)
        {
            if (amount <= 0)
                continue;

            GiveCurrency(user, currency, amount);
        }

        return true;
    }

    private bool IsHeldInHands(EntityUid user, EntityUid item)
    {
        if (!_ents.TryGetComponent(user, out HandsComponent? hands))
            return false;

        foreach (var handId in hands.Hands.Keys)
        {
            if (_hands.TryGetHeldItem((user, hands), handId, out var heldEntity) &&
                heldEntity.HasValue &&
                heldEntity.Value == item)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsDirectChildOf(EntityUid root, EntityUid item) =>
        _ents.TryGetComponent(item, out TransformComponent? xform) && xform.ParentUid == root;

    private bool IsProtectedFromDirectSale(EntityUid root, EntityUid item)
    {
        if (!_ents.HasComponent<InventoryComponent>(root))
            return false;

        if (!IsDirectChildOf(root, item))
            return false;

        if (IsHeldInHands(root, item))
            return false;

        return _ents.HasComponent<ClothingComponent>(item);
    }


    public Dictionary<string, int> GetMassSellValue(
        NcStoreComponent store,
        EntityUid container
    ) =>
        ComputeMassSellPlan(store, container).IncomeByCurrency;


    public bool TrySpawnProduct(string protoId, EntityUid user)
    {
        try
        {
            var coords = _ents.GetComponent<TransformComponent>(user).Coordinates;
            var spawned = _ents.SpawnEntity(protoId, coords);
            if (_ents.HasComponent<HandsComponent>(user))
                _hands.TryPickupAnyHand(user, spawned, false);
            return true;
        }
        catch (Exception e)
        {
            Sawmill.Error($"Spawn failed for {protoId}: {e}");
            return false;
        }
    }


    public readonly record struct MassSellStep(
        StoreListingPrototype Listing,
        string CurrencyId,
        int UnitPrice,
        int Count);

    public readonly record struct MassSellPlan(
        Dictionary<string, int> IncomeByCurrency,
        Dictionary<string, int> UnitsByListingId,
        Dictionary<string, (string CurrencyId, int UnitPrice)> PriceByListingId,
        List<MassSellStep> Steps);

    public readonly record struct InventorySnapshot(
        Dictionary<string, int> ProtoCounts,
        Dictionary<string, int> AncestorCounts,
        Dictionary<string, int> StackTypeCounts);
}
