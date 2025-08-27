using Robust.Shared.Prototypes;

namespace Content.Shared._ADT.Turrets.Components;

[RegisterComponent]
public sealed partial class TurretControllableComponent : Component
{
    [ViewVariables]
    public EntityUid? Controller;

    [DataField("ControlReturnActionEntity")]
    public EntityUid? ControlReturnActEntity;

    [DataField("ControlReturnAction")]
    public EntProtoId ControlReturnAction = "ControlReturnAction";

    [DataField("isDrone")]
    public bool IsDrone;

    [DataField("isMoveable")]
    public bool IsMoveable;

    [DataField("Range")]
    public float Range = 50f;

    [ViewVariables]
    public EntityUid? User;
    [DataField] public bool UseMouseRotation = true;
}
