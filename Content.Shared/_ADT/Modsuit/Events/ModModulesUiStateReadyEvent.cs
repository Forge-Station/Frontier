namespace Content.Shared._ADT.Modsuit.Events;

public sealed class ModModulesUiStateReadyEvent : EntityEventArgs
{
    public Dictionary<NetEntity, BoundUserInterfaceState?> States = new();  // ADT Mech UI Fix
}
