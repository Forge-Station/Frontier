using Robust.Shared.GameStates;

namespace Content.Shared.Stunnable;
 // Forge-Change full (refactory b.y. wizard)
[RegisterComponent, NetworkedComponent, Access(typeof(SharedStunSystem))]
public sealed partial class StunnedComponent : Component;
