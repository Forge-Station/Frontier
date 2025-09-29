using Robust.Shared.Serialization;

namespace Content.Shared._ADT.Modsuit.Events;

[Serializable, NetSerializable]
public sealed class ModLockMessage : BoundUserInterfaceMessage
{
    public NetEntity Module;

    public ModLockMessage(NetEntity module)
    {
        Module = module;
    }
}
