using Robust.Shared.GameStates;

namespace Content.Shared._ADT.Wires.Components;

[RegisterComponent]
[NetworkedComponent]
[AutoGenerateComponentState]
public sealed partial class ItemSlotsRequirePanelComponent : Component
{
    [DataField("slots"), AutoNetworkedField]
    public Dictionary<string, bool> Slots = new();
}

