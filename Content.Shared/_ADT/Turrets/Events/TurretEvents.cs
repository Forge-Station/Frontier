using Content.Shared.Actions;

namespace Content.Shared._ADT.Turrets.Events;

public sealed partial class ControlReturnActionEvent : InstantActionEvent
{
}

public sealed class ReturnToBodyTurretEvent : EntityEventArgs
{
    public EntityUid TurretController;

    public ReturnToBodyTurretEvent(EntityUid turretcontroller)
    {
        TurretController = turretcontroller;
    }
}

public sealed class GettingControlledEvent : EntityEventArgs
{
    public EntityUid Controller;
    public EntityUid User;

    public GettingControlledEvent(EntityUid user, EntityUid controller)
    {
        User = user;
        Controller = controller;
    }
}
