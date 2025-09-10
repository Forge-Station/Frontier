using Content.Shared.Actions;
using Content.Shared._Forge.Mech.Components;
using Content.Shared.Mech;
using Content.Shared.Mech.EntitySystems;

namespace Content.Shared._Forge.Mech.EntitySystems;

public abstract class SharedMechEquipmentSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MechOverloadComponent, SetupMechUserEvent>(SetupOverloadUser);
    }

    private void SetupOverloadUser(EntityUid uid, MechOverloadComponent comp, ref SetupMechUserEvent args)
    {
        var pilot = args.Pilot;
        _actions.AddAction(pilot, ref comp.MechOverloadActionEntity, comp.MechOverloadAction, uid);
    }
}