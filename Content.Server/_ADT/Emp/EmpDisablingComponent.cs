namespace Content.Server._ADT.Emp;

[RegisterComponent]
public sealed partial class EmpDisablingComponent : Component
{
    [DataField]
    public TimeSpan DisablingTime;
}
