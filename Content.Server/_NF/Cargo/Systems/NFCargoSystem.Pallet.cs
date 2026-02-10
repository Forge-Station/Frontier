using Content.Server.Cargo.Components;
using Content.Server._NF.Cargo.Components;
using Content.Shared._NF.Bank.Components;
using Content.Shared._NF.Cargo.BUI;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Events;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using System.Numerics;
using Content.Shared.Coordinates;
using Content.Shared._Mono.ItemTax.Components; // Forge-change: take Mono:782
using Content.Server._NF.Bank; // Forge-change: take Mono:782
using Content.Shared._NF.Bank.BUI; // Forge-change: take Mono:782
using Robust.Shared.Toolshed.Commands.Math; // Forge-change: take Mono:782

namespace Content.Server._NF.Cargo.Systems;

/// <summary>
/// Handles cargo pallet (sale) mechanics.
/// Based off of Wizden's CargoSystem.
/// </summary>
public sealed partial class NFCargoSystem
{
    // The maximum distance from the console to look for pallets.
    private const int DefaultPalletDistance = 8;

    private static readonly SoundPathSpecifier ApproveSound = new("/Audio/Effects/Cargo/ping.ogg");

    private void InitializeShuttle()
    {
        SubscribeLocalEvent<NFCargoPalletConsoleComponent, CargoPalletSellMessage>(OnPalletSale);
        SubscribeLocalEvent<NFCargoPalletConsoleComponent, CargoPalletAppraiseMessage>(OnPalletAppraise);
        SubscribeLocalEvent<NFCargoPalletConsoleComponent, BoundUIOpenedEvent>(OnPalletUIOpen);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    #region Console

    private void UpdatePalletConsoleInterface(Entity<NFCargoPalletConsoleComponent> ent) // Frontier: EntityUid<Entity
    {
        if (Transform(ent).GridUid is not EntityUid gridUid)
        {
            _ui.SetUiState(ent.Owner, CargoPalletConsoleUiKey.Sale,
            new NFCargoPalletConsoleInterfaceState(0, 0, false));
            return;
        }

        // Modify prices based on modifier.
        GetPalletGoods(ent, gridUid, out var toSell, out var amount, out var noModAmount, out var blackMarketTaxAmount, out var frontierTaxAmount, out var nfsdTaxAmount, out var medicalTaxAmount); // Forge-change: take Mono taxes
        if (TryComp<MarketModifierComponent>(ent, out var priceMod))
        {
            amount *= priceMod.Mod;
        }
        amount += noModAmount;

        _ui.SetUiState(ent.Owner, CargoPalletConsoleUiKey.Sale,
            new NFCargoPalletConsoleInterfaceState((int)amount, toSell.Count, true));
    }

    private void OnPalletUIOpen(Entity<NFCargoPalletConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdatePalletConsoleInterface(ent);
    }

    /// <summary>
    /// Ok so this is just the same thing as opening the UI, its a refresh button.
    /// I know this would probably feel better if it were like predicted and dynamic as pallet contents change
    /// However.
    /// I dont want it to explode if cargo uses a conveyor to move 8000 pineapple slices or whatever, they are
    /// known for their entity spam i wouldnt put it past them
    /// </summary>

    private void OnPalletAppraise(Entity<NFCargoPalletConsoleComponent> ent, ref CargoPalletAppraiseMessage args)
    {
        UpdatePalletConsoleInterface(ent);
    }

    #endregion

    #region Shuttle

    /// <summary>
    /// Calculates distance between two EntityCoordinates
    /// Used to check for cargo pallets around the console instead of on the grid.
    /// </summary>
    /// <param name="point1">first point to get distance between</param>
    /// <param name="point2">second point to get distance between</param>
    /// <returns></returns>
    public static double CalculateDistance(EntityCoordinates point1, EntityCoordinates point2)
    {
        var xDifference = point2.X - point1.X;
        var yDifference = point2.Y - point1.Y;

        return Math.Sqrt(xDifference * xDifference + yDifference * yDifference);
    }

    /// GetCargoPallets(gridUid, BuySellType.Sell) to return only Sell pads
    /// GetCargoPallets(gridUid, BuySellType.Buy) to return only Buy pads
    private List<(EntityUid Entity, CargoPalletComponent Component, TransformComponent PalletXform)> GetCargoPallets(EntityUid consoleUid, EntityUid gridUid, BuySellType requestType = BuySellType.All)
    {
        _pads.Clear();

        if (!TryComp(consoleUid, out TransformComponent? consoleXform))
            return _pads;

        var query = AllEntityQuery<CargoPalletComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var compXform))
        {
            // Short-path easy checks
            if (compXform.ParentUid != gridUid
                || !compXform.Anchored
                || (requestType & comp.PalletType) == 0)
            {
                continue;
            }

            // Check distance on pallets
            var distance = CalculateDistance(compXform.Coordinates, consoleXform.Coordinates);
            var maxPalletDistance = DefaultPalletDistance;

            // Get the mapped checking distance from the console
            if (TryComp<NFCargoPalletConsoleComponent>(consoleUid, out var cargoShuttleComponent))
                maxPalletDistance = cargoShuttleComponent.PalletDistance;

            if (distance > maxPalletDistance)
                continue;

            _pads.Add((uid, comp, compXform));

        }

        return _pads;
    }
    #endregion

    #region Station

    private bool SellPallets(Entity<NFCargoPalletConsoleComponent> consoleUid, EntityUid gridUid, out double amount, out double noMultiplierAmount, out double blackMarketTaxAmount, out double frontierTaxAmount, out double nfsdTaxAmount, out double medicalTaxAmount) // Forge-change: take Mono taxes // Frontier: first arg to Entity, add noMultiplierAmount
    {
        GetPalletGoods(consoleUid, gridUid, out var toSell, out amount, out noMultiplierAmount, out blackMarketTaxAmount, out frontierTaxAmount, out nfsdTaxAmount, out medicalTaxAmount); // Forge-change: take Mono taxes

        Log.Debug($"Cargo sold {toSell.Count} entities for {amount} (plus {noMultiplierAmount} without mods). (Taxes: Black Market: {blackMarketTaxAmount}, Outpost: {frontierTaxAmount}, NFSD: {nfsdTaxAmount}, Medical: {medicalTaxAmount})"); // Forge-change: take Mono taxes

        if (toSell.Count == 0)
            return false;

        var ev = new NFEntitySoldEvent(toSell, gridUid);
        RaiseLocalEvent(ref ev);

        foreach (var ent in toSell)
            Del(ent);

        return true;
    }

    private void GetPalletGoods(Entity<NFCargoPalletConsoleComponent> consoleUid, EntityUid gridUid, out HashSet<EntityUid> toSell, out double amount, out double noMultiplierAmount, out double blackMarketTaxAmount, out double frontierTaxAmount, out double nfsdTaxAmount, out double medicalTaxAmount) // Forge-change: take Mono taxes // Frontier: first arg to Entity, add noMultiplierAmount
    {
        amount = 0;
        noMultiplierAmount = 0;
        blackMarketTaxAmount = 0; // Forge-change: take Mono taxes
        frontierTaxAmount = 0; // Forge-change: take Mono taxes
        nfsdTaxAmount = 0; // Forge-change: take Mono taxes
        medicalTaxAmount = 0; // Forge-change: take Mono taxes
        toSell = new HashSet<EntityUid>();

        foreach (var (palletUid, _, _) in GetCargoPallets(consoleUid, gridUid, BuySellType.Sell))
        {
            // Containers should already get the sell price of their children so can skip those.
            _setEnts.Clear();

            _lookup.GetEntitiesIntersecting(palletUid, _setEnts,
                LookupFlags.Dynamic | LookupFlags.Sundries);

            foreach (var ent in _setEnts)
            {
                // Dont sell:
                // - anything already being sold
                // - anything anchored (e.g. light fixtures)
                // - anything blacklisted (e.g. players).
                if (toSell.Contains(ent) ||
                    _xformQuery.TryGetComponent(ent, out var xform) &&
                    (xform.Anchored || !CanSell(ent, xform)))
                {
                    continue;
                }

                if (_whitelist.IsWhitelistFail(consoleUid.Comp.Whitelist, ent))
                    continue;

                if (_blacklistQuery.HasComponent(ent))
                    continue;

                var price = _pricing.GetPrice(ent);
                if (price == 0)
                    continue;
                toSell.Add(ent);

                // Check for items that are immune to market modifiers
                if (HasComp<IgnoreMarketModifierComponent>(ent))
                    noMultiplierAmount += price;
                else
                    amount += price;
                // Forge-change-start: Mono: ItemTaxs to budgets.
                if (TryComp<ItemTaxComponent>(ent, out var itemTax))
                {
                    foreach (var (account, taxCoeff) in itemTax.TaxAccounts)
                    {
                        switch (account)
                        {
                            case SectorBankAccount.Invalid:
                                blackMarketTaxAmount += price * taxCoeff;
                                break;
                            case SectorBankAccount.Frontier:
                                frontierTaxAmount += price * taxCoeff;
                                break;
                            case SectorBankAccount.Nfsd:
                                nfsdTaxAmount += price * taxCoeff;
                                break;
                            case SectorBankAccount.Medical:
                                medicalTaxAmount += price * taxCoeff;
                                break;
                            default:
                                break;
                        }
                    }
                }
                // Forge-change-end: End Mono
            }
        }
    }

    private bool CanSell(EntityUid uid, TransformComponent xform)
    {
        // Look for blacklisted items and stop the selling of the container.
        if (_blacklistQuery.HasComponent(uid))
            return false;

        // Allow selling dead mobs
        if (_mobQuery.TryComp(uid, out var mob) && mob.CurrentState != MobState.Dead)
            return false;

        // NOTE: no bounties for now
        // var complete = IsBountyComplete(uid, out var bountyEntities);

        // Recursively check for mobs at any point.
        var children = xform.ChildEnumerator;
        while (children.MoveNext(out var child))
        {
            // NOTE: no bounties for now
            // if (complete && bountyEntities.Contains(child))
            //     continue;

            if (!CanSell(child, _xformQuery.GetComponent(child)))
                return false;
        }

        return true;
    }

    private void OnPalletSale(Entity<NFCargoPalletConsoleComponent> ent, ref CargoPalletSellMessage args)
    {
        if (!TryComp(ent, out TransformComponent? xform))
            return;

        if (xform.GridUid is not EntityUid gridUid)
        {
            _ui.SetUiState(ent.Owner, CargoPalletConsoleUiKey.Sale,
            new NFCargoPalletConsoleInterfaceState(0, 0, false));
            return;
        }

        if (!SellPallets(ent, gridUid, out var price, out var noMultiplierPrice, out var blackMarketTaxAmount, out var frontierTaxAmount, out var nfsdTaxAmount, out var medicalTaxAmount)) // Forge-change: take Mono taxes
            return;

        // Handle market modifiers & immune objects
        if (TryComp<MarketModifierComponent>(ent, out var priceMod))
        {
            price *= priceMod.Mod;
        }
        price += noMultiplierPrice;
        // Mono Begin
        if (blackMarketTaxAmount > 0)
            _bank.TrySectorDeposit(SectorBankAccount.Invalid, (int)blackMarketTaxAmount, LedgerEntryType.BlackMarketSales);
        if (frontierTaxAmount > 0)
            _bank.TrySectorDeposit(SectorBankAccount.Frontier, (int)frontierTaxAmount, LedgerEntryType.FrontierSales);
        if (nfsdTaxAmount > 0)
            _bank.TrySectorDeposit(SectorBankAccount.Nfsd, (int)nfsdTaxAmount, LedgerEntryType.NFSDSales);
        if (medicalTaxAmount > 0)
            _bank.TrySectorDeposit(SectorBankAccount.Medical, (int)medicalTaxAmount, LedgerEntryType.MedicalSales);
        if (blackMarketTaxAmount < 0)
        {
            blackMarketTaxAmount = -blackMarketTaxAmount;
            _bank.TrySectorWithdraw(SectorBankAccount.Invalid, (int)blackMarketTaxAmount, LedgerEntryType.BlackMarketPenalties);
        }
        if (frontierTaxAmount < 0)
        {
            frontierTaxAmount = -frontierTaxAmount;
            _bank.TrySectorWithdraw(SectorBankAccount.Frontier, (int)frontierTaxAmount, LedgerEntryType.FrontierPenalties);
        }
        if (nfsdTaxAmount < 0)
        {
            nfsdTaxAmount = -nfsdTaxAmount;
            _bank.TrySectorWithdraw(SectorBankAccount.Nfsd, (int)nfsdTaxAmount, LedgerEntryType.NFSDPenalties);
        }
        if (medicalTaxAmount < 0)
        {
            medicalTaxAmount = -medicalTaxAmount;
            _bank.TrySectorWithdraw(SectorBankAccount.Medical, (int)medicalTaxAmount, LedgerEntryType.MedicalPenalties);
        }
        // Mono End
        var stackPrototype = _proto.Index(ent.Comp.CashType);
        var stackUid = _stack.Spawn((int)price, stackPrototype, args.Actor.ToCoordinates());
        if (!_hands.TryPickupAnyHand(args.Actor, stackUid))
            _transform.SetLocalRotation(stackUid, Angle.Zero); // Orient these to grid north instead of map north
        _audio.PlayPvs(ApproveSound, ent);
        UpdatePalletConsoleInterface(ent);
    }

    #endregion
}

/// <summary>
/// Event broadcast raised by-ref before it is sold and
/// deleted but after the price has been calculated.
/// </summary>
[ByRefEvent]
public readonly record struct NFEntitySoldEvent(HashSet<EntityUid> Sold, EntityUid Grid);
