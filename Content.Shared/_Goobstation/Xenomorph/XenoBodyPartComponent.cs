using Robust.Shared.GameStates;

namespace Content.Shared.Goobstation.Xenomorph;

/// <summary>
/// Used to prevent doing normal surgeries on xeno bodyparts and vice versa.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenoBodyPartComponent : Component;
