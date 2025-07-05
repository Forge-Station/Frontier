using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Forge.Mercenary.Components;

[RegisterComponent]
public sealed partial class MercenaryBountyConsoleComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string BountyLabelId = "PaperMercenaryBountyManifest"; // TODO: make some paper

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string BountyCrateId = "CrateMercenaryBounty"; // TODO: make some paper

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer))]
    public TimeSpan NextPrintTime = TimeSpan.Zero;

    [DataField]
    public TimeSpan PrintDelay = TimeSpan.FromSeconds(5);

    [DataField]
    public SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    [DataField]
    public SoundSpecifier SpawnChestSound = new SoundPathSpecifier("/Audio/Effects/Lightning/lightningbolt.ogg");

    [DataField]
    public SoundSpecifier SkipSound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");

    [DataField]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_two.ogg");
}

[NetSerializable, Serializable]
public sealed class MercenaryBountyConsoleState : BoundUserInterfaceState
{
    public List<MercenaryBountyData> Bounties;
    public TimeSpan UntilNextSkip;

    public MercenaryBountyConsoleState(List<MercenaryBountyData> bounties, TimeSpan untilNextSkip)
    {
        Bounties = bounties;
        UntilNextSkip = untilNextSkip;
    }
}

//TODO: inherit this from the base message
[Serializable, NetSerializable]
public sealed class MercenaryBountyAcceptMessage : BoundUserInterfaceMessage
{
    public string BountyId;

    public MercenaryBountyAcceptMessage(string bountyId)
    {
        BountyId = bountyId;
    }
}

[Serializable, NetSerializable]
public sealed class MercenaryBountySkipMessage : BoundUserInterfaceMessage
{
    public string BountyId;

    public MercenaryBountySkipMessage(string bountyId)
    {
        BountyId = bountyId;
    }
}
