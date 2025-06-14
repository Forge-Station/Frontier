using Robust.Shared.Serialization;

namespace Content.Shared.Corvax.Mercenary;

[NetSerializable, Serializable]
public enum MercenaryConsoleUiKey : byte
{
    Bounty,
    BountyRedemption
}

[NetSerializable, Serializable]
public enum MercenaryPalletConsoleUiKey : byte
{
    Sale
}

public abstract class SharedMercenarySystem : EntitySystem {}
