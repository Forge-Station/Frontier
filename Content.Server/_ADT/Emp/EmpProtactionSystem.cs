using Content.Shared._ADT.Emp;
using Content.Shared.Containers.ItemSlots;

namespace Content.Server._ADT.Emp;

public sealed class EmpProtactionSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _slot = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<EmpContainerProtactionComponent, ItemSlotInsertAttemptEvent>(OnInserted);
        SubscribeLocalEvent<EmpContainerProtactionComponent, ItemSlotEjectedEvent>(OnEjected);
        SubscribeLocalEvent<EmpContainerProtactionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<EmpContainerProtactionComponent, MapInitEvent>(OnInit);
    }

    private void OnInserted(EntityUid uid,
        EmpContainerProtactionComponent component,
        ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled)
            return;
        EnsureComp<EmpAdtProtectionComponent>(args.Item);
        component.BatteryUid = args.Item;
    }

    private void OnEjected(EntityUid uid, EmpContainerProtactionComponent component, ref ItemSlotEjectedEvent args)
    {
        if (args.Cancelled)
            return;
        RemComp<EmpAdtProtectionComponent>(args.Item);
        component.BatteryUid = null;
    }

    private void OnShutdown(EntityUid uid, EmpContainerProtactionComponent component, ComponentShutdown args)
    {
        if (component.BatteryUid == null)
            return;
        RemComp<EmpAdtProtectionComponent>(component.BatteryUid.Value);
    }

    private void OnInit(EntityUid uid, EmpContainerProtactionComponent component, MapInitEvent args)
    {
        var battery = _slot.GetItemOrNull(uid, component.ContainerId);
        if (battery == null)
            return;
        EnsureComp<EmpAdtProtectionComponent>(battery.Value);
    }
}
