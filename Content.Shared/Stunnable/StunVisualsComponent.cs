using Robust.Shared.Utility;

namespace Content.Shared.Stunnable;

// Forge-Change full (refactory b.y. wizard)

/// <summary>
/// This is used to listen to incoming events from the AppearanceSystem
/// </summary>
[RegisterComponent]
public sealed partial class StunVisualsComponent : Component
{
    [DataField]
    public ResPath StarsPath = new ("Mobs/Effects/stunned.rsi");

    [DataField]
    public string State = "stunned";
}
