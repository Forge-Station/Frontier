using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.Mercenary.Events;

[Serializable, NetSerializable]
public sealed class MercenaryBountyRedemptionMessage : BoundUserInterfaceMessage
{
    public MercenaryBountyRedemptionMessage()
    {
    }
}
