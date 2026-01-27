using Robust.Shared.GameStates;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared._Forge.Overlord.Components;

/// <summary>
/// If a player has a Overlord they will get this component to prevent conversion.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OverlordComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<SecurityIconPrototype> OverlordStatusIcon = "OverlordIcon";
}
