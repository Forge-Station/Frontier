using Robust.Shared.Serialization;

namespace Content.Shared._Forge.Miners.Events;

[Serializable, NetSerializable]
public sealed class MinersBountyRedemptionMessage : BoundUserInterfaceMessage
{
    public MinersBountyRedemptionMessage()
    {
    }
}
