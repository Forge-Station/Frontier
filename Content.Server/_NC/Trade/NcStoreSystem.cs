using System.Linq;
using System.Numerics;
using Content.Server.Storage.Components;
using Content.Shared._NC.Trade;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Movement.Pulling.Components;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;


namespace Content.Server._NC.Trade;


public sealed class NcStoreSystem : EntitySystem
{
    private const float MaxUseDistance = 2.5f;
    private const string ReadyListingSuffix = "__ready";
    private const string CrateListingSuffix = "__crate";

    [Dependency] private readonly AccessReaderSystem _accessReader = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly NcStoreLogicSystem _logic = default!;
    [Dependency] private readonly StoreStructuredSystem _storeUi = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NcStoreComponent, StoreBuyListingBoundUiMessage>(OnBuyRequest);
        SubscribeLocalEvent<NcStoreComponent, StoreSellListingBoundUiMessage>(OnSellRequest);
        SubscribeLocalEvent<NcStoreComponent, StoreMassSellPulledCrateBoundUiMessage>(OnMassSellPulledCrateRequest);
    }

    public bool CanUseStore(EntityUid store, NcStoreComponent comp, EntityUid user)
    {
        if (!Exists(user))
            return false;

        if (TryComp(store, out AccessReaderComponent? reader))
        {
            if (!_accessReader.IsAllowed(user, store, reader))
                return false;
        }

        return true;
    }

    private bool IsInRange(EntityUid a, EntityUid b, float maxDistance)
    {
        if (!_entMan.TryGetComponent(a, out TransformComponent? aXf))
            return false;
        if (!_entMan.TryGetComponent(b, out TransformComponent? bXf))
            return false;

        var aPos = _transform.GetWorldPosition(aXf);
        var bPos = _transform.GetWorldPosition(bXf);

        return Vector2.Distance(aPos, bPos) <= maxDistance;
    }

    private bool IsInUseRange(EntityUid store, EntityUid user) => IsInRange(store, user, MaxUseDistance);

    private bool CanInteract(EntityUid store, NcStoreComponent comp, EntityUid user)
    {
        if (!CanUseStore(store, comp, user))
            return false;

        return IsInUseRange(store, user);
    }

    private static bool TryParseSellListingId(
        string rawId,
        out string listingId,
        out bool fromCrate
    )
    {
        fromCrate = false;
        listingId = rawId;

        if (string.IsNullOrEmpty(listingId))
            return false;

        if (listingId.EndsWith(CrateListingSuffix, StringComparison.Ordinal))
        {
            fromCrate = true;
            listingId = listingId[..^CrateListingSuffix.Length];
        }

        if (listingId.EndsWith(ReadyListingSuffix, StringComparison.Ordinal))
            listingId = listingId[..^ReadyListingSuffix.Length];

        return !string.IsNullOrEmpty(listingId);
    }


    private void OnBuyRequest(EntityUid uid, NcStoreComponent comp, StoreBuyListingBoundUiMessage msg)
    {
        if (comp.CurrentUser is not { } actor)
            return;

        if (!CanInteract(uid, comp, actor))
            return;


        var listing = comp.Listings.FirstOrDefault(x => x.Id == msg.ListingId && x.Mode == StoreMode.Buy);
        if (listing == null)
            return;

        var count = Math.Max(1, msg.Count);
        if (!_logic.TryBuy(listing.Id, uid, comp, actor, count))
            return;

        _audio.PlayPvs("/Audio/Effects/Cargo/ping.ogg", uid, AudioParams.Default.WithVolume(-2f));
        _storeUi.UpdateUiState(uid, comp, actor);
    }

    private void OnSellRequest(EntityUid uid, NcStoreComponent comp, StoreSellListingBoundUiMessage msg)
    {
        if (comp.CurrentUser is not { } actor)
            return;

        if (!CanInteract(uid, comp, actor))
            return;

        if (!TryParseSellListingId(msg.ListingId, out var requestedId, out var fromCrate))
            return;

        var listing = comp.Listings.FirstOrDefault(x => x.Id == requestedId && x.Mode == StoreMode.Sell);
        if (listing == null)
            return;

        var count = Math.Max(1, msg.Count);

        bool ok;

        if (fromCrate)
        {
            if (!_entMan.TryGetComponent(actor, out PullerComponent? puller) ||
                puller.Pulling is not { } crate)
                return;

            if (!_entMan.TryGetComponent(crate, out EntityStorageComponent? storage) || storage.Open)
                return;

            const float maxCrateDistance = 2f;
            if (!IsInRange(uid, crate, maxCrateDistance))
                return;

            ok = _logic.TrySellFromContainer(listing.Id, uid, comp, actor, crate, count);
        }
        else
            ok = _logic.TrySell(listing.Id, uid, comp, actor, count);

        if (!ok)
            return;

        _audio.PlayPvs("/Audio/Effects/Cargo/ping.ogg", uid, AudioParams.Default.WithVolume(-2f));
        _storeUi.UpdateUiState(uid, comp, actor);
    }


    private void OnMassSellPulledCrateRequest(
        EntityUid uid,
        NcStoreComponent comp,
        StoreMassSellPulledCrateBoundUiMessage msg
    )
    {
        if (comp.CurrentUser is not { } actor)
            return;

        if (!CanInteract(uid, comp, actor))
            return;

        if (!_entMan.TryGetComponent(actor, out PullerComponent? puller) ||
            puller.Pulling is not { } crate)
            return;

        if (!_entMan.TryGetComponent(crate, out EntityStorageComponent? storage))
            return;

        if (storage.Open)
            return;

        const float maxDistance = 2f;
        if (!IsInRange(uid, crate, maxDistance))
            return;

        if (!_logic.TryMassSellFromContainer(uid, comp, actor, crate))
            return;

        _audio.PlayPvs("/Audio/Effects/Cargo/ping.ogg", uid);
        _storeUi.UpdateUiState(uid, comp, actor);
    }
}
