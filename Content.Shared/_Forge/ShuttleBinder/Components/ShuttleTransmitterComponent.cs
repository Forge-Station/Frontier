using Robust.Shared.GameStates;

namespace Content.Shared._Forge.ShuttleBinder.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShuttleTransmitterComponent : Component
{
    [ViewVariables]
    public EntityUid? LinkedBeacon;
}
