using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Forge.ExtendedPinpointer;

/// <summary>
/// Displays a sprite on the item that points towards the target component.
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState]
[Access(typeof(SharedExtendedPinpointerSystem))]
public sealed partial class ExtendedPinpointerComponent : Component
{
    // TODO: Type serializer oh god
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public string? TargetId;

    [DataField("component"), ViewVariables(VVAccess.ReadWrite)]
    public string? Whitelist = "BecomesStation";

    [DataField("mediumDistance"), ViewVariables(VVAccess.ReadWrite)]
    public float MediumDistance = 16f;

    [DataField("closeDistance"), ViewVariables(VVAccess.ReadWrite)]
    public float CloseDistance = 8f;

    [DataField("reachedDistance"), ViewVariables(VVAccess.ReadWrite)]
    public float ReachedDistance = 1f;

    /// <summary>
    ///     Pinpointer arrow precision in radians.
    /// </summary>
    [DataField("precision"), ViewVariables(VVAccess.ReadWrite)]
    public double Precision = 0.09;

    /// <summary>
    ///     Name to display of the target being tracked.
    /// </summary>
    [DataField("targetName"), ViewVariables(VVAccess.ReadWrite)]
    public string? TargetName;

    /// <summary>
    ///     Whether or not the target name should be updated when the target is updated.
    /// </summary>
    [DataField("updateTargetName"), ViewVariables(VVAccess.ReadWrite)]
    public bool UpdateTargetName;

    /// <summary>
    ///     Whether or not the target can be reassigned.
    /// </summary>
    [DataField("canRetarget"), ViewVariables(VVAccess.ReadWrite)]
    public bool CanRetarget = false;

    [ViewVariables]
    public EntityUid? Target = null;

    [ViewVariables, AutoNetworkedField]
    public bool IsActive = false;

    [ViewVariables, AutoNetworkedField]
    public Angle ArrowAngle;

    [ViewVariables, AutoNetworkedField]
    public Distance DistanceToTarget = Distance.Unknown;

    [ViewVariables]
    public bool HasTarget => DistanceToTarget != Distance.Unknown;

    // Frontier - if greater than 0, then the pinpointer stops pointing to the target when it's further than this value
    [DataField("maxRange")]
    public float MaxRange = -1;

    // Frontier - time in seconds to retarget
    [DataField("retargetDoAfter")]
    public float RetargetDoAfter = 15f;

    // Frontier - whether this pinpointer can target mobs
    [DataField("canTargetMobs")]
    public bool CanTargetMobs = false;

    // Whether this pinpointer's target knows about the pinpointer using the PinpointerTargetComponent.
    [DataField("setsTarget")]
    public bool SetsTarget = false;
}

[Serializable, NetSerializable]
public enum Distance : byte
{
    Unknown,
    Reached,
    Close,
    Medium,
    Far
}
