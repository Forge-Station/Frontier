using Robust.Shared.Serialization;

namespace Content.Shared._Forge.Mercenary.Events;

[Serializable, NetSerializable]
public sealed class MercenaryBountyRedemptionMessage : BoundUserInterfaceMessage
{
    public MercenaryBountyRedemptionMessage()
    {
    }
}
