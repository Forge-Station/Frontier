using Robust.Shared.GameStates;

namespace Content.Shared._Forge.Overlord.Components;

/// <summary>
/// Component given to an entity to mark it is a Overlord implant.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class OverlordImplantComponent : Component;
