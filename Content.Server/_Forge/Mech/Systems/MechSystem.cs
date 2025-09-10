using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Robust.Server.GameObjects;
using Content.Server.Emp;
using Content.Shared.Mech.Equipment.Components;

namespace Content.Server.Mech.Systems;

/// <inheritdoc/>
public sealed partial class MechSystem
{
    public override void UpdateUserInterfaceByEquipment(EntityUid equipmentUid)
    {
        base.UpdateUserInterfaceByEquipment(equipmentUid);

        if (!TryComp<MechEquipmentComponent>(equipmentUid, out var comp))
        {
            Log.Error("Could not find mech equipment owner to update UI.");
            return;
        }
        if (!comp.EquipmentOwner.HasValue)
            return;
        UpdateUserInterface(comp.EquipmentOwner.Value);
    }
}