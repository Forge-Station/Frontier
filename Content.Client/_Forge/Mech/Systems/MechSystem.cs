using System.Linq;
using Content.Client._Forge.Mech.UI;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Client.Mech;

/// <inheritdoc/>
public sealed partial class MechSystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private void InitializeForge()
    {
        SubscribeLocalEvent<MechComponent, MechToggleEquipmentEvent>(OnToggleEquipmentAction);
        SubscribeLocalEvent<MechComponent, CloseMechMenuEvent>(OnMechExit);
        SubscribeLocalEvent<MechComponent, PopulateMechEquipmentMenuEvent>(OnPopulate);
    }

    private void OnToggleEquipmentAction(EntityUid uid, MechComponent component, MechToggleEquipmentEvent args)
    {
        if (!TryComp<MechPilotComponent>(_playerManager.LocalEntity, out var pilot) || pilot.Mech != uid)
            return;
        if (args.Handled)
            return;

        args.Handled = true;

        if (!_timing.IsFirstTimePredicted)
            return;

        var controller = _ui.GetUIController<MechEquipmentUIController>();
        controller.ToggleMenu();
        controller.PopulateMenu(component.EquipmentContainer.ContainedEntities.Select(x => GetNetEntity(x)).ToList());
    }

    private void OnMechExit(EntityUid uid, MechComponent component, CloseMechMenuEvent args)
    {
        _ui.GetUIController<MechEquipmentUIController>().CloseMenu();
    }

    private void OnPopulate(EntityUid uid, MechComponent component, PopulateMechEquipmentMenuEvent args)
    {
        var controller = _ui.GetUIController<MechEquipmentUIController>();
        controller.PopulateMenu(args.Equipment);
    }
}