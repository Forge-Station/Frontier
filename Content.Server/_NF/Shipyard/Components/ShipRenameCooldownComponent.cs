using Robust.Shared.Serialization;

namespace Content.Server._NF.Shipyard.Components;

/// <summary>
/// Component that tracks when a player last renamed a ship.
/// This is used to implement a cooldown on the rename feature.
/// </summary>
[RegisterComponent]
public sealed partial class ShipyardRenameCooldownComponent : Component
{
    /// <summary>
    /// How long the player must wait between rename actions.
    /// </summary>
    [DataField]
    public TimeSpan CooldownDuration = TimeSpan.FromMinutes(10);

    /// <summary>
    /// When the player can next rename a ship.
    /// </summary>
    [DataField]
    public TimeSpan NextRenameTime;
}
