using Robust.Shared.GameStates;

namespace Content.Shared._Forge.ShuttleBinder.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ShuttleBeaconComponent : Component
{
    [ViewVariables]
    public bool Active = true;
}
