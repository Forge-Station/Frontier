using Robust.Shared.Serialization;

namespace Content.Shared._ADT.Modsuit.Events;

[Serializable, NetSerializable]
public sealed class ModModuleRemoveMessage : BoundUserInterfaceMessage
{
    public NetEntity Module;

    public ModModuleRemoveMessage(NetEntity module)
    {
        Module = module;
    }
}
