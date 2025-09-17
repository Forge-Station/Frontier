using Content.Shared.Inventory;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Clothing;

[RegisterComponent] [NetworkedComponent] [AutoGenerateComponentState]
[Access(typeof(SharedMagbootsSystem))]
public sealed partial class JumpbootsComponent : Component
{
    [DataField("jumpAction")]
    public EntProtoId Action = "ActionJumpboots";

    [DataField] [AutoNetworkedField]
    public EntityUid? ActionEntity;

    public SlotFlags AllowedSlots = SlotFlags.FEET;

    [DataField("jumpStrength")]
    public float Strength = 13f;

    /// <summary>
    /// Volume control for the spell.
    /// </summary>
    [DataField("jumpVolume")]
    public float Volume = 1f;
}
