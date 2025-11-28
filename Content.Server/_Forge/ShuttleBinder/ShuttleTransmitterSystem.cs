using Content.Shared._Forge.ShuttleBinder.Components;
using Content.Shared.Shuttles.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Interaction;
using Content.Server.Shuttles.Components;

namespace Content.Server._Forge.ShuttleBinder;

public sealed class ShuttleTransmitterSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ShuttleTransmitterComponent, ComponentStartup>(OnTransmitterStartup);
        SubscribeLocalEvent<ShuttleTransmitterComponent, ComponentShutdown>(OnTransmitterShutdown);
        SubscribeLocalEvent<ShuttleTransmitterComponent, ActivateInWorldEvent>(OnTransmitterActivate);
        SubscribeLocalEvent<ShuttleTransmitterComponent, EntParentChangedMessage>(OnParentChanged);
    }

    private void OnParentChanged(EntityUid uid, ShuttleTransmitterComponent component, ref EntParentChangedMessage args)
    {
        if (args.Transform.GridUid != null && HasComp<ShuttleComponent>(args.Transform.GridUid.Value))
        {
            UpdateShuttleTarget(uid, component.LinkedBeacon);
        }
        else if (args.OldParent != null && args.OldParent.Value.IsValid() &&
                 TryComp<TransformComponent>(args.OldParent, out var oldXform) &&
                 oldXform.GridUid != null && HasComp<ShuttleComponent>(oldXform.GridUid.Value))
        {
            UpdateShuttleTarget(uid, null);
        }
    }

    private void OnTransmitterStartup(EntityUid uid, ShuttleTransmitterComponent component, ComponentStartup args)
    {
        if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid == null)
            return;

        var shuttleUid = xform.GridUid.Value;
        var activeTransmitter = FindActiveTransmitter(shuttleUid);

        if (activeTransmitter != null && activeTransmitter != uid)
        {
            component.LinkedBeacon = null;
            _popup.PopupCoordinates("Another transmitter is already active on this shuttle. Activate this one to make it active.", xform.Coordinates);
        }
        else
        {
            UpdateShuttleTarget(uid, component.LinkedBeacon);
        }
    }

    private void OnTransmitterShutdown(EntityUid uid, ShuttleTransmitterComponent component, ComponentShutdown args)
    {
        if (component.LinkedBeacon != null)
        {
            if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid == null)
                return;

            var shuttleUid = xform.GridUid.Value;
            var nextTransmitter = FindNextTransmitter(shuttleUid, uid);

            if (nextTransmitter != null && TryComp<ShuttleTransmitterComponent>(nextTransmitter, out var nextComponent))
            {
                UpdateShuttleTarget(nextTransmitter.Value, nextComponent.LinkedBeacon);
            }
            else
            {
                if (TryComp<ShuttleComponent>(shuttleUid, out var shuttle))
                {
                    shuttle.TargetPOI = null;
                    Dirty(shuttleUid, shuttle);
                }
            }
        }
    }

    private void OnTransmitterActivate(EntityUid uid, ShuttleTransmitterComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<TransformComponent>(uid, out var xform) || xform.GridUid == null)
            return;

        var shuttleUid = xform.GridUid.Value;

        if (component.LinkedBeacon != null)
        {
            UnlinkFromBeacon(uid, component);
            _popup.PopupEntity("Transmitter unlinked from beacon", args.User);

            var nextTransmitter = FindNextTransmitter(shuttleUid, uid);
            if (nextTransmitter != null && TryComp<ShuttleTransmitterComponent>(nextTransmitter, out var nextComponent))
            {
                UpdateShuttleTarget(nextTransmitter.Value, nextComponent.LinkedBeacon);
            }
        }
        else
        {
            MakeTransmitterActive(uid, component, shuttleUid);
            _popup.PopupEntity("Transmitter activated", args.User);
        }

        args.Handled = true;
    }

    private void MakeTransmitterActive(EntityUid transmitterUid, ShuttleTransmitterComponent component, EntityUid shuttleUid)
    {
        var query = EntityQueryEnumerator<ShuttleTransmitterComponent, TransformComponent>();
        while (query.MoveNext(out var otherUid, out var otherComponent, out var otherXform))
        {
            if (otherXform.GridUid == shuttleUid && otherUid != transmitterUid)
            {
                if (TryComp<ShuttleComponent>(shuttleUid, out var shuttle))
                {
                    shuttle.TargetPOI = null;
                    Dirty(shuttleUid, shuttle);
                }
            }
        }

        UpdateShuttleTarget(transmitterUid, component.LinkedBeacon);
    }

    private EntityUid? FindActiveTransmitter(EntityUid shuttleUid)
    {
        var query = EntityQueryEnumerator<ShuttleTransmitterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var xform))
        {
            if (xform.GridUid == shuttleUid && component.LinkedBeacon != null)
                return uid;
        }
        return null;
    }

    private EntityUid? FindNextTransmitter(EntityUid shuttleUid, EntityUid excludeUid)
    {
        var query = EntityQueryEnumerator<ShuttleTransmitterComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var component, out var xform))
        {
            if (xform.GridUid == shuttleUid && uid != excludeUid && component.LinkedBeacon != null)
                return uid;
        }
        return null;
    }

    public void LinkToBeacon(EntityUid transmitterUid, EntityUid beaconUid, ShuttleTransmitterComponent? component = null)
    {
        if (!Resolve(transmitterUid, ref component))
            return;

        component.LinkedBeacon = beaconUid;

        if (TryComp<TransformComponent>(transmitterUid, out var xform) && xform.GridUid != null)
        {
            MakeTransmitterActive(transmitterUid, component, xform.GridUid.Value);
        }

        Dirty(transmitterUid, component);
    }

    public void UnlinkFromBeacon(EntityUid transmitterUid, ShuttleTransmitterComponent? component = null)
    {
        if (!Resolve(transmitterUid, ref component))
            return;

        component.LinkedBeacon = null;
        UpdateShuttleTarget(transmitterUid, null);
        Dirty(transmitterUid, component);
    }

    private void UpdateShuttleTarget(EntityUid transmitterUid, EntityUid? beaconUid)
    {
        if (!TryComp<TransformComponent>(transmitterUid, out var xform) || xform.GridUid == null)
            return;

        var shuttleUid = xform.GridUid.Value;

        if (!TryComp<ShuttleComponent>(shuttleUid, out var shuttle))
            return;

        if (beaconUid != null)
        {
            var stationUid = FindStationForBeacon(beaconUid.Value);
            if (stationUid != null)
            {
                shuttle.TargetPOI = stationUid.Value.ToString();
                _popup.PopupCoordinates($"Shuttle target set", xform.Coordinates);
            }
            else
            {
                shuttle.TargetPOI = null;
            }
        }
        else
        {
            shuttle.TargetPOI = null;
        }

        //Dirty(shuttleUid, shuttle);
    }

    private EntityUid? FindStationForBeacon(EntityUid beaconUid)
    {
        if (!TryComp<TransformComponent>(beaconUid, out var beaconXform) || beaconXform.GridUid == null)
            return null;

        var gridUid = beaconXform.GridUid.Value;

        return gridUid;
    }
}
