namespace Content.Shared._ADT.Turrets.Components;

[RegisterComponent]
public sealed partial class TurretControllerComponent : Component
{
    [ViewVariables]
    public EntityUid? CurrentTurret;

    [ViewVariables]
    public EntityUid? CurrentUser;
}
