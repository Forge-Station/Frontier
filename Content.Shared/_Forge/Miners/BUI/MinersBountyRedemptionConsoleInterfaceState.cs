using Robust.Shared.Serialization;

namespace Content.Shared._Forge.Miners.BUI;

[NetSerializable, Serializable]
public sealed class MinersBountyRedemptionConsoleInterfaceState : BoundUserInterfaceState
{
    public bool Success;

    public string Message;

    public MinersBountyRedemptionConsoleInterfaceState(bool success, string message)
    {
        Success = success;
        Message = message;
    }
}
