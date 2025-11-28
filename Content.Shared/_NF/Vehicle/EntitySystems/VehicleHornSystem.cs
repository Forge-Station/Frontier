using Content.Shared._NF.Vehicle.Components;
using Content.Shared.Actions;
using Content.Shared._Goobstation.Vehicles;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._NF.Vehicle.EntitySystems;

public sealed class VehicleHornSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<VehicleHornComponent, ComponentInit>(OnVehicleHornInit);
        SubscribeLocalEvent<VehicleHornComponent, ComponentShutdown>(OnVehicleHornShutdown);
    }

    /// Horn-only functions
    private void OnVehicleHornShutdown(EntityUid uid, VehicleHornComponent component, ComponentShutdown args)
    {
        // Perf: If the entity is deleting itself, no reason to change these back.
        if (Terminating(uid))
            return;

        _actions.RemoveAction(uid, component.ActionEntity);
    }

    private void OnVehicleHornInit(EntityUid uid, VehicleHornComponent component, ComponentInit args)
    {
        _actions.AddAction(uid, ref component.ActionEntity, out var _, component.Action);
    }
}
