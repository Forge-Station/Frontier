using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Forge.Miners.Components;
using Content.Shared.Labels.EntitySystems;
using Content.Shared._NF.Bank;
using Content.Shared._Forge.Miners;
using Content.Shared._Forge.Miners.Components;
using Content.Shared._Forge.Miners.Prototypes;
using Content.Shared._Forge.Miners.Events;
using Content.Shared.Access.Components;
using Content.Shared.Database;
using Content.Shared.NameIdentifier;
using Content.Shared.Paper;
using JetBrains.Annotations;
using Robust.Shared.Containers;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Content.Shared.Stacks;
using Content.Server.Cargo.Systems;

namespace Content.Server._NF.Cargo.Systems; // Needs to collide with base namespace

public sealed partial class NFCargoSystem
{
    [ValidatePrototypeId<NameIdentifierGroupPrototype>]
    private const string MinersBountyNameIdentifierGroup = "Bounty"; // Use the bounty name ID group (0-999) for now.

    private EntityQuery<MinersBountyLabelComponent> _minersBountyLabelQuery;

    private void InitializeMinersBounty()
    {
        SubscribeLocalEvent<MinersBountyConsoleComponent, BoundUIOpenedEvent>(OnMinersBountyConsoleOpened);
        SubscribeLocalEvent<MinersBountyConsoleComponent, MinersBountyAcceptMessage>(OnMinersBountyAccept);
        SubscribeLocalEvent<MinersBountyConsoleComponent, MinersBountySkipMessage>(OnSkipMinersBountyMessage);

        SubscribeLocalEvent<MinersBountyRedemptionConsoleComponent, MinersBountyRedemptionMessage>(OnRedeemBounty);

        SubscribeLocalEvent<MinersBountyDatabaseComponent, ComponentAdd>(OnBountyDbAdded);

        _minersBountyLabelQuery = GetEntityQuery<MinersBountyLabelComponent>();
    }

    private void OnMinersBountyConsoleOpened(EntityUid uid, MinersBountyConsoleComponent component, BoundUIOpenedEvent args)
    {
        var service = _sectorService.GetServiceEntity();
        var gridUid = Transform(uid).GridUid;

        if (gridUid == null)
            return;

        if (!TryComp<MinersBountyDatabaseComponent>(gridUid, out var bountyDb))
            return;

        var untilNextSkip = bountyDb.NextSkipTime - _timing.CurTime;
        _ui.SetUiState(uid, MinersConsoleUiKey.Bounty, new MinersBountyConsoleState(bountyDb.Bounties, untilNextSkip));
    }

    private void OnMinersBountyAccept(EntityUid uid, MinersBountyConsoleComponent component, MinersBountyAcceptMessage args)
    {
        if (_timing.CurTime < component.NextPrintTime)
            return;

        var service = _sectorService.GetServiceEntity();
        var gridUid = Transform(uid).GridUid;

        if (gridUid == null)
            return;

        if (!TryComp<MinersBountyDatabaseComponent>(gridUid, out var bountyDb))
            return;

        if (!TryGetMinersBountyFromId(service, args.BountyId, out var bounty, bountyDb))
            return;

        var bountyObj = bounty.Value;

        if (bountyObj.Accepted || !_proto.TryIndex(bountyObj.Bounty, out var bountyPrototype))
            return;

        MinersBountyData bountyData = new MinersBountyData(bountyPrototype!, bountyObj.Id, true);

        TryOverwriteMinersBountyFromId(service, bountyData, bountyDb); // bountyDb

        if (bountyPrototype.SpawnChest)
        {
            var chest = Spawn(component.BountyCrateId, Transform(uid).Coordinates);
            SetupMinersBountyChest(chest, bountyData, bountyPrototype);
            _audio.PlayPvs(component.SpawnChestSound, uid);
        }
        else
        {
            var label = Spawn(component.BountyLabelId, Transform(uid).Coordinates);
            SetupMinersBountyManifest(label, bountyData, bountyPrototype);
            _audio.PlayPvs(component.PrintSound, uid);
        }

        component.NextPrintTime = _timing.CurTime + component.PrintDelay;
        UpdateMinersBountyConsoles(bountyDb);
    }

    private void OnSkipMinersBountyMessage(EntityUid uid, MinersBountyConsoleComponent component, MinersBountySkipMessage args)
    {
        var service = _sectorService.GetServiceEntity();
        var gridUid = Transform(uid).GridUid;

        if (gridUid == null)
            return;

        if (!TryComp<MinersBountyDatabaseComponent>(gridUid, out var db))
            return;

        if (_timing.CurTime < db.NextSkipTime)
            return;

        if (!TryGetMinersBountyFromId(service, args.BountyId, out var bounty, db))
            return;

        if (args.Actor is not { Valid: true } mob)
            return;

        if (TryComp<AccessReaderComponent>(uid, out var accessReaderComponent) &&
            !_accessReader.IsAllowed(mob, uid, accessReaderComponent))
        {
            _audio.PlayPvs(component.DenySound, uid);
            return;
        }

        if (!TryRemoveMinersBounty(service, bounty.Value.Id, db))
            return;

        FillMinersBountyDatabase(service, db);
        if (bounty.Value.Accepted)
            db.NextSkipTime = _timing.CurTime + db.SkipDelay;
        else
            db.NextSkipTime = _timing.CurTime + db.CancelDelay;

        var untilNextSkip = db.NextSkipTime - _timing.CurTime;
        _ui.SetUiState(uid, MinersConsoleUiKey.Bounty, new MinersBountyConsoleState(db.Bounties, untilNextSkip));
        _audio.PlayPvs(component.SkipSound, uid);
    }

    private void SetupMinersBountyChest(EntityUid uid, MinersBountyData bounty, MinersBountyPrototype prototype)
    {
        _meta.SetEntityName(uid, Loc.GetString("miners-bounty-chest-name", ("id", bounty.Id)));

        FormattedMessage message = new FormattedMessage();
        message.TryAddMarkup(Loc.GetString("miners-bounty-chest-description-start"), out var _);
        foreach (var entry in prototype.Entries)
        {
            message.PushNewline();
            message.TryAddMarkup($"- {Loc.GetString("miners-bounty-console-manifest-entry",
                ("amount", entry.Amount),
                ("item", Loc.GetString(entry.Name)))}", out var _);
        }
        message.PushNewline();
        message.TryAddMarkup(Loc.GetString("miners-bounty-console-manifest-reward", ("reward", BankSystemExtensions.ToSpesoString(prototype.Reward))), out var _);

        _meta.SetEntityDescription(uid, message.ToMarkup());

        if (TryComp<MinersBountyLabelComponent>(uid, out var label))
            label.Id = bounty.Id;
    }

    private void SetupMinersBountyManifest(EntityUid uid, MinersBountyData bounty, MinersBountyPrototype prototype, PaperComponent? paper = null)
    {
        _meta.SetEntityName(uid, Loc.GetString("miners-bounty-manifest-name", ("id", bounty.Id)));

        if (!Resolve(uid, ref paper))
            return;

        var msg = new FormattedMessage();
        msg.AddText(Loc.GetString("miners-bounty-manifest-header", ("id", bounty.Id)));
        msg.PushNewline();
        msg.AddText(Loc.GetString("miners-bounty-manifest-list-start"));
        msg.PushNewline();
        foreach (var entry in prototype.Entries)
        {
            msg.TryAddMarkup($"- {Loc.GetString("miners-bounty-console-manifest-entry",
                ("amount", entry.Amount),
                ("item", Loc.GetString(entry.Name)))}", out var _);
            msg.PushNewline();
        }
        msg.TryAddMarkup(Loc.GetString("miners-bounty-console-manifest-reward", ("reward", BankSystemExtensions.ToSpesoString(prototype.Reward))), out var _);
        _paper.SetContent((uid, paper), msg.ToMarkup());
    }

    private bool TryGetMinersBountyLabel(EntityUid uid,
        [NotNullWhen(true)] out EntityUid? labelEnt,
        [NotNullWhen(true)] out MinersBountyLabelComponent? labelComp)
    {
        labelEnt = null;
        labelComp = null;
        if (!_containerQuery.TryGetComponent(uid, out var containerMan))
            return false;

        // make sure this label was actually applied to a crate.
        if (!_container.TryGetContainer(uid, LabelSystem.ContainerName, out var container, containerMan))
            return false;

        if (container.ContainedEntities.FirstOrNull() is not { } label ||
            !_minersBountyLabelQuery.TryGetComponent(label, out var component))
            return false;

        labelEnt = label;
        labelComp = component;
        return true;
    }

    private void OnBountyDbAdded(EntityUid uid, MinersBountyDatabaseComponent component, ComponentAdd args)
    {
        FillMinersBountyDatabase(uid, component);
    }

    public void FillMinersBountyDatabase(EntityUid serviceId, MinersBountyDatabaseComponent? component = null)
    {
        if (!Resolve(serviceId, ref component))
            return;

        while (component?.Bounties.Count < component?.MaxBounties)
        {
            if (!TryAddMinersBounty(serviceId, component))
                break;
        }

        UpdateMinersBountyConsoles(component);
    }

    [PublicAPI]
    public bool TryAddMinersBounty(EntityUid serviceId, MinersBountyDatabaseComponent? component = null)
    {
        if (!Resolve(serviceId, ref component))
            return false;

        var allBounties = _proto.EnumeratePrototypes<MinersBountyPrototype>().ToList();
        var filteredBounties = new List<MinersBountyPrototype>();
        foreach (var proto in allBounties)
        {
            if (component.Bounties.Any(b => b.Bounty == proto.ID))
                continue;
            filteredBounties.Add(proto);
        }

        var pool = filteredBounties.Count == 0 ? allBounties : filteredBounties;
        var bounty = _random.Pick(pool);
        return TryAddMinersBounty(serviceId, bounty, component);
    }

    [PublicAPI]
    public bool TryAddMinersBounty(EntityUid serviceId, string bountyId, MinersBountyDatabaseComponent? component = null)
    {
        if (!_proto.TryIndex<MinersBountyPrototype>(bountyId, out var bounty))
            return false;

        return TryAddMinersBounty(serviceId, bounty, component);
    }

    public bool TryAddMinersBounty(EntityUid serviceId, MinersBountyPrototype bounty, MinersBountyDatabaseComponent? component = null)
    {
        if (!Resolve(serviceId, ref component))
            return false;

        if (component.Bounties.Count >= component.MaxBounties)
            return false;

        _nameIdentifier.GenerateUniqueName(serviceId, MinersBountyNameIdentifierGroup, out var randomVal); // Need a string ID for internal name, probably doesn't need to be outward facing.
        component.Bounties.Add(new MinersBountyData(bounty, randomVal, false));
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"Added miners bounty \"{bounty.ID}\" (id:{component.TotalBounties}) to service {ToPrettyString(serviceId)}");
        component.TotalBounties++;
        return true;
    }

    [PublicAPI]
    public bool TryRemoveMinersBounty(EntityUid serviceId, string dataId, MinersBountyDatabaseComponent? component = null)
    {
        if (!TryGetMinersBountyFromId(serviceId, dataId, out var data, component))
            return false;

        return TryRemoveMinersBounty(serviceId, data.Value, component);
    }

    public bool TryRemoveMinersBounty(EntityUid serviceId, MinersBountyData data, MinersBountyDatabaseComponent? component = null)
    {
        if (!Resolve(serviceId, ref component))
            return false;

        for (var i = 0; i < component.Bounties.Count; i++)
        {
            if (component.Bounties[i].Id == data.Id)
            {
                component.Bounties.RemoveAt(i);
                return true;
            }
        }

        return false;
    }

    public bool TryGetMinersBountyFromId(
        EntityUid uid,
        string id,
        [NotNullWhen(true)] out MinersBountyData? bounty,
        MinersBountyDatabaseComponent? component = null)
    {
        bounty = null;
        if (!Resolve(uid, ref component))
            return false;

        foreach (var bountyData in component.Bounties)
        {
            if (bountyData.Id != id)
                continue;
            bounty = bountyData;
            break;
        }

        return bounty != null;
    }

    private bool TryOverwriteMinersBountyFromId(
        EntityUid uid,
        MinersBountyData bounty,
        MinersBountyDatabaseComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        for (int i = 0; i < component.Bounties.Count; i++)
        {
            if (bounty.Id == component.Bounties[i].Id)
            {
                component.Bounties[i] = bounty;
                return true;
            }
        }
        return false;
    }

    public void UpdateMinersBountyConsoles(MinersBountyDatabaseComponent? db = null)
    {
        if (db == null)
            return;

        var gridUid = db.Owner;
        var query = EntityQueryEnumerator<MinersBountyConsoleComponent, UserInterfaceComponent>();

        while (query.MoveNext(out var uid, out _, out var ui))
        {
            if (Transform(uid).GridUid != gridUid)
                continue;
            var untilNextSkip = db.NextSkipTime - _timing.CurTime;
            _ui.SetUiState((uid, ui), MinersConsoleUiKey.Bounty, new MinersBountyConsoleState(db.Bounties, untilNextSkip));
        }
    }

    private void OnRedeemBounty(EntityUid uid, MinersBountyRedemptionConsoleComponent component, MinersBountyRedemptionMessage args)
    {
        var amount = 0;

        if (component.LastRedeemAttempt + _redemptionDelay > _timing.CurTime)
            return;

        EntityUid gridUid = Transform(uid).GridUid ?? EntityUid.Invalid;
        if (gridUid == EntityUid.Invalid)
            return;

        if (!TryComp<MinersBountyDatabaseComponent>(gridUid, out var bountyDb))
            return;

        MinersBountyEntitySearchState bountySearchState = new MinersBountyEntitySearchState();

        foreach (var bounty in bountyDb.Bounties)
        {
            if (bounty.Accepted)
            {
                if (!_proto.TryIndex(bounty.Bounty, out var bountyPrototype))
                    continue;
                if (bountyPrototype.SpawnChest)
                {
                    var newState = new MinersBountyState(bounty, bountyPrototype);
                    foreach (var entry in bountyPrototype.Entries)
                    {
                        newState.Entries[entry.Name] = 0;
                    }
                    bountySearchState.CrateBounties[bounty.Id] = newState;
                }
                else
                {
                    var newState = new MinersBountyState(bounty, bountyPrototype);
                    foreach (var entry in bountyPrototype.Entries)
                    {
                        newState.Entries[entry.Name] = 0;
                    }
                    bountySearchState.LooseObjectBounties[bounty.Id] = newState;
                }
            }
        }

        // 2. Iterate over bounty pads, find all tagged, non-tagged items.
        foreach (var (palletUid, _) in GetContrabandPallets(gridUid))
        {
            foreach (var ent in _lookup.GetEntitiesIntersecting(palletUid,
                         LookupFlags.Dynamic | LookupFlags.Sundries | LookupFlags.Approximate | LookupFlags.Sensors))
            {
                // Dont match:
                // - anything anchored (e.g. light fixtures)
                // Checks against already handled set done by CheckEntityForPirateBounties
                if (_xformQuery.TryGetComponent(ent, out var xform) &&
                    xform.Anchored)
                    continue;

                CheckEntityForMinersBounties(ent, ref bountySearchState);
            }
        }

        // 4. When done, note all completed bounties.  Remove them from the list of accepted bounties, and spawn the rewards.
        bool bountiesRemoved = false;
        string redeemedBounties = string.Empty;
        foreach (var (id, bounty) in bountySearchState.CrateBounties)
        {
            bool bountyMet = true;
            var prototype = bounty.Prototype;
            foreach (var entry in prototype.Entries)
            {
                if (!bounty.Entries.ContainsKey(entry.Name) ||
                    entry.Amount > bounty.Entries[entry.Name])
                {
                    bountyMet = false;
                    break;
                }
            }

            if (bountyMet)
            {
                bountiesRemoved = true;
                redeemedBounties = Loc.GetString("miners-bounty-redemption-append", ("bounty", id), ("empty", string.IsNullOrEmpty(redeemedBounties) ? 0 : 1), ("prev", redeemedBounties));

                TryRemoveMinersBounty(_sectorService.GetServiceEntity(), id, bountyDb); // bountyDb
                amount += prototype.Reward;
                foreach (var entity in bounty.Entities)
                {
                    Del(entity);
                }
            }
        }

        foreach (var (id, bounty) in bountySearchState.LooseObjectBounties)
        {
            bool bountyMet = true;
            var prototype = bounty.Prototype;
            foreach (var entry in prototype.Entries)
            {
                if (!bounty.Entries.ContainsKey(entry.Name) ||
                    entry.Amount > bounty.Entries[entry.Name])
                {
                    bountyMet = false;
                    break;
                }
            }

            if (bountyMet)
            {
                bountiesRemoved = true;
                redeemedBounties = Loc.GetString("miners-bounty-redemption-append", ("bounty", id), ("empty", string.IsNullOrEmpty(redeemedBounties) ? 0 : 1), ("prev", redeemedBounties));

                TryRemoveMinersBounty(_sectorService.GetServiceEntity(), id, bountyDb); // bountyDb
                amount += prototype.Reward;
                foreach (var entity in bounty.Entities)
                {
                    Del(entity);
                }
            }
        }

        if (amount > 0)
        {
            _stack.SpawnMultiple("MinersToken", amount, Transform(uid).Coordinates);
            _audio.PlayPvs(component.AcceptSound, uid);
            _popup.PopupEntity(Loc.GetString("miners-bounty-redemption-success", ("bounties", redeemedBounties), ("amount", amount)), args.Actor);
        }
        else
        {
            _audio.PlayPvs(component.DenySound, uid);
            _popup.PopupEntity(Loc.GetString("miners-bounty-redemption-deny"), args.Actor);
        }

        // Bounties removed, restore database list
        if (bountiesRemoved)
        {
            FillMinersBountyDatabase(_sectorService.GetServiceEntity(), bountyDb); //bountyDb
        }
        component.LastRedeemAttempt = _timing.CurTime;
    }

    sealed class MinersBountyState
    {
        public readonly MinersBountyData Data;
        public MinersBountyPrototype Prototype;
        public HashSet<EntityUid> Entities = new();
        public Dictionary<string, int> Entries = new();
        public bool Calculating = false; // Relevant only for crate bounties (due to tree traversal)

        public MinersBountyState(MinersBountyData data, MinersBountyPrototype prototype)
        {
            Data = data;
            Prototype = prototype;
        }
    }

    sealed class MinersBountyEntitySearchState
    {
        public HashSet<EntityUid> HandledEntities = new();
        public Dictionary<string, MinersBountyState> LooseObjectBounties = new();
        public Dictionary<string, MinersBountyState> CrateBounties = new();
    }

    private void CheckEntityForMinersCrateBounty(EntityUid uid, ref MinersBountyEntitySearchState state, string id)
    {
        // Sanity check: entity previously handled, this subtree is done.
        if (state.HandledEntities.Contains(uid))
            return;

        // Add this container to the list of entities to remove.
        var bounty = state.CrateBounties[id]; // store the particular bounty we're looking up.
        if (bounty.Calculating) // Bounty check is already happening in a parent, return.
        {
            state.HandledEntities.Add(uid);
            return;
        }

        if (TryComp<ContainerManagerComponent>(uid, out var containers))
        {
            bounty.Entities.Add(uid);
            bounty.Calculating = true;

            foreach (var container in containers.Containers.Values)
            {
                foreach (var ent in container.ContainedEntities)
                {
                    // Subtree has a separate label, run check on that label
                    if (TryComp<MinersBountyLabelComponent>(ent, out var label))
                    {
                        CheckEntityForMinersCrateBounty(ent, ref state, label.Id);
                    }
                    else
                    {
                        AdjustBountyForEntity(ent, bounty);
                        state.HandledEntities.Add(ent);
                    }
                }
            }
        }
        state.HandledEntities.Add(uid);
    }

    // Return two lists: a list of non-labelled entities (nodes), and a list of labelled entities (subtrees)
    private void CheckEntityForMinersBounties(EntityUid uid, ref MinersBountyEntitySearchState state)
    {
        // Entity previously handled, this subtree is done.
        if (state.HandledEntities.Contains(uid))
            return;

        // 3a. If tagged as labelled, check contents against crate bounties.  If it satisfies any of them, note it as solved.
        if (TryComp<MinersBountyLabelComponent>(uid, out var label))
            CheckEntityForMinersCrateBounty(uid, ref state, label.Id);
        else
        {
            // 3b. If not tagged as labelled, check contents against non-create bounties.  If it satisfies any of them, increase the quantity.
            foreach (var (_, bounty) in state.LooseObjectBounties)
            {
                if (AdjustBountyForEntity(uid, bounty))
                    break;
            }
        }
        state.HandledEntities.Add(uid);
    }

    // Checks an object against a bounty, adjusts the bounty's state and returns true if it matches.
    private bool AdjustBountyForEntity(EntityUid target, MinersBountyState bounty)
    {
        foreach (var entry in bounty.Prototype.Entries)
        {
            // Should add an assertion here, entry.Name should exist.
            // Entry already fulfilled, skip this entity.
            if (bounty.Entries[entry.Name] >= entry.Amount)
            {
                continue;
            }

            // Check whitelists for the pirate bounty.
            if (TryComp<MinersBountyItemComponent>(target, out var targetBounty) && targetBounty.ID == entry.ID)
            {
                if (TryComp<StackComponent>(target, out var stack))
                    bounty.Entries[entry.Name] += stack.Count;
                else
                    bounty.Entries[entry.Name]++;
                bounty.Entities.Add(target);
                return true;
            }
        }
        return false;
    }
}
