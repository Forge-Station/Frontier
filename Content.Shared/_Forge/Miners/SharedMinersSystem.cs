using Robust.Shared.Serialization;

namespace Content.Shared._Forge.Miners;

[NetSerializable, Serializable]
public enum MinersConsoleUiKey : byte
{
    Bounty,
    BountyRedemption
}

[NetSerializable, Serializable]
public enum MinersPalletConsoleUiKey : byte
{
    Sale
}

public abstract class SharedMinersSystem : EntitySystem {}
