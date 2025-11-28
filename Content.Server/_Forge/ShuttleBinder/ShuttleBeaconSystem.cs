using Content.Shared._Forge.ShuttleBinder.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Content.Shared.Interaction;

namespace Content.Server._Forge.ShuttleBinder;

public sealed class ShuttleBeaconSystem : EntitySystem
{
    [Dependency] private readonly ShuttleTransmitterSystem _transmitterSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<ShuttleBeaconComponent, ComponentStartup>(OnBeaconStartup);
        SubscribeLocalEvent<ShuttleBeaconComponent, ComponentShutdown>(OnBeaconShutdown);
        SubscribeLocalEvent<ShuttleBeaconComponent, InteractUsingEvent>(OnBeaconInteractUsing);
    }

    private void OnBeaconStartup(EntityUid uid, ShuttleBeaconComponent component, ComponentStartup args)
    {
        component.Active = true;
        Dirty(uid, component);
    }

    private void OnBeaconShutdown(EntityUid uid, ShuttleBeaconComponent component, ComponentShutdown args)
    {
        component.Active = false;

        var query = EntityQueryEnumerator<ShuttleTransmitterComponent>();
        while (query.MoveNext(out var transmitterUid, out var transmitter))
        {
            if (transmitter.LinkedBeacon == uid)
            {
                _transmitterSystem.UnlinkFromBeacon(transmitterUid, transmitter);
            }
        }
    }

    private void OnBeaconInteractUsing(EntityUid uid, ShuttleBeaconComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (TryComp<ShuttleTransmitterComponent>(args.Used, out var transmitter))
        {
            _transmitterSystem.LinkToBeacon(args.Used, uid, transmitter);
            _popup.PopupEntity($"Transmitter linked to beacon", args.User);
            args.Handled = true;
        }
    }
}
