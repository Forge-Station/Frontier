using Robust.Shared.Serialization;

namespace Content.Shared._ADT.Modsuit.Events;

[Serializable, NetSerializable]
public sealed class ModModuleDeactivateMessage : BoundUserInterfaceMessage
{
    public NetEntity Module;

    public ModModuleDeactivateMessage(NetEntity module)
    {
        Module = module;
    }
}
