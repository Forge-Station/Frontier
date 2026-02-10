using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Strip.Components;

/// <summary>
/// Give this to an entity when you want to decrease stripping times
/// </summary>
[RegisterComponent, NetworkedComponent]
[AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class ThievingComponent : Component
{
    /// <summary>
    /// How much the strip time should be shortened by
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan StripTimeReduction = TimeSpan.FromSeconds(0.5f);

    /// <summary>
    /// Should it notify the user if they're stripping a pocket?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Stealthy;

    /// <summary>
    /// Variable pointing at the Alert modal
    /// </summary>
    [DataField]
    public ProtoId<AlertPrototype> StealthyAlertProtoId = "Stealthy";

    /// <summary>
    /// Prevent component replication to clients other than the owner,
    /// doesn't affect prediction.
    /// Get mogged.
    /// </summary>
    public override bool SendOnlyToOwner => true;

    // Forge-change-start: take _Monolith 37 & 2522

    /// <summary>
    /// Mono: Multiplies the strip time.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("timeMultiplier")]
    [AutoNetworkedField]
    public float TimeMultiplier = 1f;

    /// <summary>
    /// Mono: If true, this entity can identify hidden strip slots.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite)]
    [DataField("identifyHidden")]
    [AutoNetworkedField]
    public bool IdentifyHidden;
    // Forge-change-end
}

/// <summary>
/// Event raised to toggle the thieving component.
/// </summary>
public sealed partial class ToggleThievingEvent : BaseAlertEvent;

