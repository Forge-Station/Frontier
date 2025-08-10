using Content.Shared.Shuttles.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Shuttles.Components;

/// <summary>
/// This component manages stealth properties for a shuttle, such as cloaking duration and cooldown.
/// It is intended to be used alongside <see cref="IFFComponent"/> and its <see cref="IFFFlags.Hide"/> flag.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedShuttleSystem))]
public sealed partial class ShuttleStealthComponent : Component
{
    /// <summary>
    /// When the full stealth functionality will automatically turn off.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan? HideEndTime;

    /// <summary>
    /// When the full stealth functionality can be used again.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField]
    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan? HideCooldownEndTime;
}
