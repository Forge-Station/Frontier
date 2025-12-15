using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Vocalization.Components;

/// <summary>
/// Makes an entity vocalize at set intervals
/// </summary>
[RegisterComponent]
[AutoGenerateComponentPause]
public sealed partial class VocalizerComponent : Component
{
    /// <summary>
    /// Minimum time to wait after speaking to vocalize again
    /// </summary>
    [DataField]
    public TimeSpan MinVocalizeInterval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// Maximum time to wait after speaking to vocalize again
    /// </summary>
    [DataField]
    public TimeSpan MaxVocalizeInterval = TimeSpan.FromMinutes(6);

    /// <summary>
    /// Next time at which to vocalize
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    [AutoPausedField]
    public TimeSpan NextVocalizeInterval = TimeSpan.Zero;

    /// <summary>
    /// If true, messages spoken by this vocalizer will not be logged in the chat window
    /// and will only be shown as speech bubbles.
    /// </summary>
    [DataField]
    public bool HideChat;
    //Forge-Change-Start
    /// <summary>
    /// How close a player has to be for the entity to vocalize.
    /// If null, it will always vocalize regardless of player proximity.
    /// </summary>
    [DataField]
    public float? PlayerDistance = 50f;

    /// <summary>
    /// If true, the entity will only vocalize if a player is on the same grid.
    /// Only has an effect if PlayerDistance is not null.
    /// </summary>
    [DataField]
    public bool RequireSameGrid = true;
    //Forge-Change-End
}
