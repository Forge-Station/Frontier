using Content.Shared._ADT.Emp;
using Content.Shared.Containers.ItemSlots;

namespace Content.Server._ADT.Emp;

public sealed class EmpProtactionSystem : EntitySystem
{
    [Dependency] private readonly ItemSlotsSystem _slot = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<_ADT.Emp.EmpContainerProtactionComponent, ItemSlotInsertAttemptEvent>(OnInserted);
        SubscribeLocalEvent<_ADT.Emp.EmpContainerProtactionComponent, ItemSlotEjectedEvent>(OnEjected);
        SubscribeLocalEvent<_ADT.Emp.EmpContainerProtactionComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<_ADT.Emp.EmpContainerProtactionComponent, MapInitEvent>(OnInit);
    }
    private void OnInserted(EntityUid uid, _ADT.Emp.EmpContainerProtactionComponent component, ref ItemSlotInsertAttemptEvent args)
    {
        if (args.Cancelled)
            return;
        EnsureComp<EmpProtectionComponent>(args.Item);
        component.BatteryUid = args.Item;
    }
    private void OnEjected(EntityUid uid, _ADT.Emp.EmpContainerProtactionComponent component, ref ItemSlotEjectedEvent args)
    {
        if (args.Cancelled)
            return;
        RemComp<EmpProtectionComponent>(args.Item);
        component.BatteryUid = null;
    }
    private void OnShutdown(EntityUid uid, _ADT.Emp.EmpContainerProtactionComponent component, ComponentShutdown args)
    {
        if (component.BatteryUid == null)
            return;
        RemComp<EmpProtectionComponent>(component.BatteryUid.Value);
    }
    private void OnInit(EntityUid uid, _ADT.Emp.EmpContainerProtactionComponent component, MapInitEvent args)
    {
        var battery = _slot.GetItemOrNull(uid, component.ContainerId);
        if (battery == null)
            return;
        EnsureComp<EmpProtectionComponent>(battery.Value);
    }
}
