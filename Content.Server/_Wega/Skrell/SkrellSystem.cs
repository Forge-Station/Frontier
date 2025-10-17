using Content.Shared.Inventory.Events;
using Content.Shared.Skrell;
using Content.Shared.Inventory;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.MagicMirror;
using Robust.Shared.Timing;

namespace Content.Server.Skrell;

public sealed class SkrellSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventorySystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SkrellComponent, DidEquipEvent>(OnEquip);
        SubscribeLocalEvent<HairMarkingRemovedEvent>(OnRemoveSlot);
    }

    private void OnEquip(Entity<SkrellComponent> entity, ref DidEquipEvent args)
    {
        var slot = args.Slot;
        if (slot == "pocket3")
        {
            var item = args.Equipee;
            if (CheckCondition(entity))
            {
                Timer.Spawn(1, () => _inventorySystem.TryUnequip(item, slot, force: true));
            }
        }
    }

    private bool CheckCondition(EntityUid uid)
    {
        if (TryComp<HumanoidAppearanceComponent>(uid, out var humanoid))
        {
            if (!humanoid.MarkingSet.TryGetCategory(MarkingCategories.Hair, out var hairMarkings) || hairMarkings.Count == 0)
                return true;
        }
        return false;
    }

    private void OnRemoveSlot(HairMarkingRemovedEvent args)
    {
        var target = GetEntity(args.Target);
        if (!HasComp<SkrellComponent>(target))
            return;

        if (_inventorySystem.TryGetSlotEntity(target, "pocket3", out _))
        {
            _inventorySystem.TryUnequip(target, "pocket3");
        }
    }
}
