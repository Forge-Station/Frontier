using Robust.Shared.Serialization;

namespace Content.Shared._Forge.Mercenary.BUI;

[NetSerializable, Serializable]
public sealed class MercenaryBountyRedemptionConsoleInterfaceState : BoundUserInterfaceState
{
    public bool Success;

    public string Message;

    public MercenaryBountyRedemptionConsoleInterfaceState(bool success, string message)
    {
        Success = success;
        Message = message;
    }
}
