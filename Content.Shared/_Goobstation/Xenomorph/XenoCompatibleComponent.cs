using Robust.Shared.GameStates;

namespace Content.Shared.Goobstation.Xenomorph;

/// <summary>
/// Component added to a body that enables surgeries to create xeno organ slots.
/// This lets you become a xeno-catboy hybrid.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenoCompatibleComponent : Component;
