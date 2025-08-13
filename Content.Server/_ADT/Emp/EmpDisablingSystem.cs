using Content.Server.Emp;

namespace Content.Server._ADT.Emp;

public sealed class EmpDisablingSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<_ADT.Emp.EmpDisablingComponent, EmpPulseEvent>(OnEmpPulse);
    }
    private void OnEmpPulse(EntityUid uid, _ADT.Emp.EmpDisablingComponent component, ref EmpPulseEvent args)
    {
        args.Disabled = true;
        args.Duration = component.DisablingTime;
    }
}
