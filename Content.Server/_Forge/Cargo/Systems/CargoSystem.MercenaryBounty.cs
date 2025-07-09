using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server._Forge.Mercenary.Components;
using Content.Shared.Labels.EntitySystems;
using Content.Shared._NF.Bank;
using Content.Shared._Forge.Mercenary;
using Content.Shared._Forge.Mercenary.Components;
using Content.Shared._Forge.Mercenary.Prototypes;
using Content.Shared._Forge.Mercenary.Events;
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
    private const string MercenaryBountyNameIdentifierGroup = "Bounty"; // Use the bounty name ID group (0-999) for now.

    private EntityQuery<MercenaryBountyLabelComponent> _mercenaryBountyLabelQuery;

    private void InitializeMercenaryBounty()
    {
        SubscribeLocalEvent<MercenaryBountyConsoleComponent, BoundUIOpenedEvent>(OnMercenaryBountyConsoleOpened);
        SubscribeLocalEvent<MercenaryBountyConsoleComponent, MercenaryBountyAcceptMessage>(OnMercenaryBountyAccept);
        SubscribeLocalEvent<MercenaryBountyConsoleComponent, MercenaryBountySkipMessage>(OnSkipMercenaryBountyMessage);

        SubscribeLocalEvent<MercenaryBountyRedemptionConsoleComponent, MercenaryBountyRedemptionMessage>(OnRedeemBounty);

        SubscribeLocalEvent<MercenaryBountyConsoleComponent, MapInitEvent>(OnMercenaryMapInit);

        _mercenaryBountyLabelQuery = GetEntityQuery<MercenaryBountyLabelComponent>();
    }

    private void OnMercenaryBountyConsoleOpened(EntityUid uid, MercenaryBountyConsoleComponent component, BoundUIOpenedEvent args)
    {
        var service = _sectorService.GetServiceEntity();
        var gridUid = Transform(uid).GridUid;

        if (gridUid == null)
            return;

        if (!TryComp<MercenaryBountyDatabaseComponent>(gridUid, out var bountyDb))
            return;

        var untilNextSkip = bountyDb.NextSkipTime - _timing.CurTime;
        _ui.SetUiState(uid, MercenaryConsoleUiKey.Bounty, new MercenaryBountyConsoleState(bountyDb.Bounties, untilNextSkip));
    }

    private void OnMercenaryBountyAccept(EntityUid uid, MercenaryBountyConsoleComponent component, MercenaryBountyAcceptMessage args)
    {
        if (_timing.CurTime < component.NextPrintTime)
            return;

        var service = _sectorService.GetServiceEntity();
        var gridUid = Transform(uid).GridUid;

        if (gridUid == null)
            return;

        if (!TryComp<MercenaryBountyDatabaseComponent>(gridUid, out var bountyDb))
            return;

        if (!TryGetMercenaryBountyFromId(service, args.BountyId, out var bounty, bountyDb))
            return;

        var bountyObj = bounty.Value;

        if (bountyObj.Accepted || !_proto.TryIndex(bountyObj.Bounty, out var bountyPrototype))
            return;

        MercenaryBountyData bountyData = new MercenaryBountyData(bountyPrototype!, bountyObj.Id, true);

        TryOverwriteMercenaryBountyFromId(service, bountyData, bountyDb); // bountyDb

        if (bountyPrototype.SpawnChest)
        {
            var chest = Spawn(component.BountyCrateId, Transform(uid).Coordinates);
            SetupMercenaryBountyChest(chest, bountyData, bountyPrototype);
            _audio.PlayPvs(component.SpawnChestSound, uid);
        }
        else
        {
            var label = Spawn(component.BountyLabelId, Transform(uid).Coordinates);
            SetupMercenaryBountyManifest(label, bountyData, bountyPrototype);
            _audio.PlayPvs(component.PrintSound, uid);
        }

        component.NextPrintTime = _timing.CurTime + component.PrintDelay;
        UpdateMercenaryBountyConsoles(bountyDb);
    }

    private void OnSkipMercenaryBountyMessage(EntityUid uid, MercenaryBountyConsoleComponent component, MercenaryBountySkipMessage args)
    {
        var service = _sectorService.GetServiceEntity();
        var gridUid = Transform(uid).GridUid;

        if (gridUid == null)
            return;

        if (!TryComp<MercenaryBountyDatabaseComponent>(gridUid, out var db))
            return;

        if (_timing.CurTime < db.NextSkipTime)
            return;

        if (!TryGetMercenaryBountyFromId(service, args.BountyId, out var bounty, db))
            return;

        if (args.Actor is not { Valid: true } mob)
            return;

        if (TryComp<AccessReaderComponent>(uid, out var accessReaderComponent) &&
            !_accessReader.IsAllowed(mob, uid, accessReaderComponent))
        {
            _audio.PlayPvs(component.DenySound, uid);
            return;
        }

        if (!TryRemoveMercenaryBounty(service, bounty.Value.Id, db))
            return;

        FillMercenaryBountyDatabase(service, db);
        if (bounty.Value.Accepted)
            db.NextSkipTime = _timing.CurTime + db.SkipDelay;
        else
            db.NextSkipTime = _timing.CurTime + db.CancelDelay;

        var untilNextSkip = db.NextSkipTime - _timing.CurTime;
        _ui.SetUiState(uid, MercenaryConsoleUiKey.Bounty, new MercenaryBountyConsoleState(db.Bounties, untilNextSkip));
        _audio.PlayPvs(component.SkipSound, uid);
    }

    private void SetupMercenaryBountyChest(EntityUid uid, MercenaryBountyData bounty, MercenaryBountyPrototype prototype)
    {
        _meta.SetEntityName(uid, Loc.GetString("mercenary-bounty-chest-name", ("id", bounty.Id)));

        FormattedMessage message = new FormattedMessage();
        message.TryAddMarkup(Loc.GetString("mercenary-bounty-chest-description-start"), out var _);
        foreach (var entry in prototype.Entries)
        {
            message.PushNewline();
            message.TryAddMarkup($"- {Loc.GetString("mercenary-bounty-console-manifest-entry",
                ("amount", entry.Amount),
                ("item", Loc.GetString(entry.Name)))}", out var _);
        }
        message.PushNewline();
        message.TryAddMarkup(Loc.GetString("mercenary-bounty-console-manifest-reward", ("reward", BankSystemExtensions.ToMercenaryTokenString(prototype.Reward))), out var _);

        _meta.SetEntityDescription(uid, message.ToMarkup());

        if (TryComp<MercenaryBountyLabelComponent>(uid, out var label))
            label.Id = bounty.Id;
    }

    private void SetupMercenaryBountyManifest(EntityUid uid, MercenaryBountyData bounty, MercenaryBountyPrototype prototype, PaperComponent? paper = null)
    {
        _meta.SetEntityName(uid, Loc.GetString("mercenary-bounty-manifest-name", ("id", bounty.Id)));

        if (!Resolve(uid, ref paper))
            return;

        var msg = new FormattedMessage();
        msg.AddText(Loc.GetString("mercenary-bounty-manifest-header", ("id", bounty.Id)));
        msg.PushNewline();
        msg.AddText(Loc.GetString("mercenary-bounty-manifest-list-start"));
        msg.PushNewline();
        foreach (var entry in prototype.Entries)
        {
            msg.TryAddMarkup($"- {Loc.GetString("mercenary-bounty-console-manifest-entry",
                ("amount", entry.Amount),
                ("item", Loc.GetString(entry.Name)))}", out var _);
            msg.PushNewline();
        }
        msg.TryAddMarkup(Loc.GetString("mercenary-bounty-console-manifest-reward", ("reward", BankSystemExtensions.ToMercenaryTokenString(prototype.Reward))), out var _);
        _paper.SetContent((uid, paper), msg.ToMarkup());
    }

    private bool TryGetMercenaryBountyLabel(EntityUid uid,
        [NotNullWhen(true)] out EntityUid? labelEnt,
        [NotNullWhen(true)] out MercenaryBountyLabelComponent? labelComp)
    {
        labelEnt = null;
        labelComp = null;
        if (!_containerQuery.TryGetComponent(uid, out var containerMan))
            return false;

        // make sure this label was actually applied to a crate.
        if (!_container.TryGetContainer(uid, LabelSystem.ContainerName, out var container, containerMan))
            return false;

        if (container.ContainedEntities.FirstOrNull() is not { } label ||
            !_mercenaryBountyLabelQuery.TryGetComponent(label, out var component))
            return false;

        labelEnt = label;
        labelComp = component;
        return true;
    }

    private void OnMercenaryMapInit(EntityUid uid, MercenaryBountyConsoleComponent component, MapInitEvent args)
    {
        var gridUid = Transform(uid).GridUid;
        if (gridUid == null)
            return;

        if (!TryComp<MercenaryBountyDatabaseComponent>(gridUid, out var bountyDb))
        {
            return;
        }

        FillMercenaryBountyDatabase(uid, bountyDb);
    }

    public void FillMercenaryBountyDatabase(EntityUid serviceId, MercenaryBountyDatabaseComponent? component = null)
    {
        if (!Resolve(serviceId, ref component))
            return;

        while (component?.Bounties.Count < component?.MaxBounties)
        {
            if (!TryAddMercenaryBounty(serviceId, component))
                break;
        }

        UpdateMercenaryBountyConsoles();
    }

    [PublicAPI]
    public bool TryAddMercenaryBounty(EntityUid serviceId, MercenaryBountyDatabaseComponent? component = null)
    {
        if (!Resolve(serviceId, ref component))
            return false;

        var allBounties = _proto.EnumeratePrototypes<MercenaryBountyPrototype>().ToList();
        var filteredBounties = new List<MercenaryBountyPrototype>();
        foreach (var proto in allBounties)
        {
            if (component.Bounties.Any(b => b.Bounty == proto.ID))
                continue;
            filteredBounties.Add(proto);
        }

        var pool = filteredBounties.Count == 0 ? allBounties : filteredBounties;
        var bounty = _random.Pick(pool);
        return TryAddMercenaryBounty(serviceId, bounty, component);
    }

    [PublicAPI]
    public bool TryAddMercenaryBounty(EntityUid serviceId, string bountyId, MercenaryBountyDatabaseComponent? component = null)
    {
        if (!_proto.TryIndex<MercenaryBountyPrototype>(bountyId, out var bounty))
            return false;

        return TryAddMercenaryBounty(serviceId, bounty, component);
    }

    public bool TryAddMercenaryBounty(EntityUid serviceId, MercenaryBountyPrototype bounty, MercenaryBountyDatabaseComponent? component = null)
    {
        if (!Resolve(serviceId, ref component))
            return false;

        if (component.Bounties.Count >= component.MaxBounties)
            return false;

        _nameIdentifier.GenerateUniqueName(serviceId, MercenaryBountyNameIdentifierGroup, out var randomVal); // Need a string ID for internal name, probably doesn't need to be outward facing.
        component.Bounties.Add(new MercenaryBountyData(bounty, randomVal, false));
        _adminLogger.Add(LogType.Action, LogImpact.Low, $"Added mercenary bounty \"{bounty.ID}\" (id:{component.TotalBounties}) to service {ToPrettyString(serviceId)}");
        component.TotalBounties++;
        return true;
    }

    [PublicAPI]
    public bool TryRemoveMercenaryBounty(EntityUid serviceId, string dataId, MercenaryBountyDatabaseComponent? component = null)
    {
        if (!TryGetMercenaryBountyFromId(serviceId, dataId, out var data, component))
            return false;

        return TryRemoveMercenaryBounty(serviceId, data.Value, component);
    }

    public bool TryRemoveMercenaryBounty(EntityUid serviceId, MercenaryBountyData data, MercenaryBountyDatabaseComponent? component = null)
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

    public bool TryGetMercenaryBountyFromId(
        EntityUid uid,
        string id,
        [NotNullWhen(true)] out MercenaryBountyData? bounty,
        MercenaryBountyDatabaseComponent? component = null)
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

    private bool TryOverwriteMercenaryBountyFromId(
        EntityUid uid,
        MercenaryBountyData bounty,
        MercenaryBountyDatabaseComponent? component = null)
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

    public void UpdateMercenaryBountyConsoles(MercenaryBountyDatabaseComponent? db = null)
    {
        var query = EntityQueryEnumerator<MercenaryBountyConsoleComponent, UserInterfaceComponent>();

        var serviceId = _sectorService.GetServiceEntity();

        if (db == null)
            return;

        while (query.MoveNext(out var uid, out _, out var ui))
        {
            var untilNextSkip = db.NextSkipTime - _timing.CurTime;
            _ui.SetUiState((uid, ui), MercenaryConsoleUiKey.Bounty, new MercenaryBountyConsoleState(db.Bounties, untilNextSkip));
        }
    }

    private void OnRedeemBounty(EntityUid uid, MercenaryBountyRedemptionConsoleComponent component, MercenaryBountyRedemptionMessage args)
    {
        var amount = 0;

        if (component.LastRedeemAttempt + _redemptionDelay > _timing.CurTime)
            return;

        EntityUid gridUid = Transform(uid).GridUid ?? EntityUid.Invalid;
        if (gridUid == EntityUid.Invalid)
            return;

        if (!TryComp<MercenaryBountyDatabaseComponent>(gridUid, out var bountyDb))
            return;

        MercenaryBountyEntitySearchState bountySearchState = new MercenaryBountyEntitySearchState();

        foreach (var bounty in bountyDb.Bounties)
        {
            if (bounty.Accepted)
            {
                if (!_proto.TryIndex(bounty.Bounty, out var bountyPrototype))
                    continue;
                if (bountyPrototype.SpawnChest)
                {
                    var newState = new MercenaryBountyState(bounty, bountyPrototype);
                    foreach (var entry in bountyPrototype.Entries)
                    {
                        newState.Entries[entry.Name] = 0;
                    }
                    bountySearchState.CrateBounties[bounty.Id] = newState;
                }
                else
                {
                    var newState = new MercenaryBountyState(bounty, bountyPrototype);
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

                CheckEntityForMercenaryBounties(ent, ref bountySearchState);
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
                redeemedBounties = Loc.GetString("mercenary-bounty-redemption-append", ("bounty", id), ("empty", string.IsNullOrEmpty(redeemedBounties) ? 0 : 1), ("prev", redeemedBounties));

                TryRemoveMercenaryBounty(_sectorService.GetServiceEntity(), id, bountyDb); // bountyDb
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
                redeemedBounties = Loc.GetString("mercenary-bounty-redemption-append", ("bounty", id), ("empty", string.IsNullOrEmpty(redeemedBounties) ? 0 : 1), ("prev", redeemedBounties));

                TryRemoveMercenaryBounty(_sectorService.GetServiceEntity(), id, bountyDb); // bountyDb
                amount += prototype.Reward;
                foreach (var entity in bounty.Entities)
                {
                    Del(entity);
                }
            }
        }

        if (amount > 0)
        {
            _stack.SpawnMultiple("MercenaryToken", amount, Transform(uid).Coordinates);
            _audio.PlayPvs(component.AcceptSound, uid);
            _popup.PopupEntity(Loc.GetString("mercenary-bounty-redemption-success", ("bounties", redeemedBounties), ("amount", amount)), args.Actor);
        }
        else
        {
            _audio.PlayPvs(component.DenySound, uid);
            _popup.PopupEntity(Loc.GetString("mercenary-bounty-redemption-deny"), args.Actor);
        }

        // Bounties removed, restore database list
        if (bountiesRemoved)
        {
            FillMercenaryBountyDatabase(_sectorService.GetServiceEntity(), bountyDb); //bountyDb
        }
        component.LastRedeemAttempt = _timing.CurTime;
    }

    sealed class MercenaryBountyState
    {
        public readonly MercenaryBountyData Data;
        public MercenaryBountyPrototype Prototype;
        public HashSet<EntityUid> Entities = new();
        public Dictionary<string, int> Entries = new();
        public bool Calculating = false; // Relevant only for crate bounties (due to tree traversal)

        public MercenaryBountyState(MercenaryBountyData data, MercenaryBountyPrototype prototype)
        {
            Data = data;
            Prototype = prototype;
        }
    }

    sealed class MercenaryBountyEntitySearchState
    {
        public HashSet<EntityUid> HandledEntities = new();
        public Dictionary<string, MercenaryBountyState> LooseObjectBounties = new();
        public Dictionary<string, MercenaryBountyState> CrateBounties = new();
    }

    private void CheckEntityForMercenaryCrateBounty(EntityUid uid, ref MercenaryBountyEntitySearchState state, string id)
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
                    if (TryComp<MercenaryBountyLabelComponent>(ent, out var label))
                    {
                        CheckEntityForMercenaryCrateBounty(ent, ref state, label.Id);
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
    private void CheckEntityForMercenaryBounties(EntityUid uid, ref MercenaryBountyEntitySearchState state)
    {
        // Entity previously handled, this subtree is done.
        if (state.HandledEntities.Contains(uid))
            return;

        // 3a. If tagged as labelled, check contents against crate bounties.  If it satisfies any of them, note it as solved.
        if (TryComp<MercenaryBountyLabelComponent>(uid, out var label))
            CheckEntityForMercenaryCrateBounty(uid, ref state, label.Id);
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
    private bool AdjustBountyForEntity(EntityUid target, MercenaryBountyState bounty)
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
            if (TryComp<MercenaryBountyItemComponent>(target, out var targetBounty) && targetBounty.ID == entry.ID)
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
