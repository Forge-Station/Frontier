using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared._Forge.Miners.Components;

[RegisterComponent]
public sealed partial class MinersBountyConsoleComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string BountyLabelId = "PaperMinersBountyManifest"; // TODO: make some paper

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string BountyCrateId = "CrateMinersBounty"; // TODO: make some paper

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
public sealed class MinersBountyConsoleState : BoundUserInterfaceState
{
    public List<MinersBountyData> Bounties;
    public TimeSpan UntilNextSkip;

    public MinersBountyConsoleState(List<MinersBountyData> bounties, TimeSpan untilNextSkip)
    {
        Bounties = bounties;
        UntilNextSkip = untilNextSkip;
    }
}

//TODO: inherit this from the base message
[Serializable, NetSerializable]
public sealed class MinersBountyAcceptMessage : BoundUserInterfaceMessage
{
    public string BountyId;

    public MinersBountyAcceptMessage(string bountyId)
    {
        BountyId = bountyId;
    }
}

[Serializable, NetSerializable]
public sealed class MinersBountySkipMessage : BoundUserInterfaceMessage
{
    public string BountyId;

    public MinersBountySkipMessage(string bountyId)
    {
        BountyId = bountyId;
    }
}
