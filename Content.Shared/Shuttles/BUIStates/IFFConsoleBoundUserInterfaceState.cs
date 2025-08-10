using Content.Shared.Shuttles.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Shuttles.BUIStates;

[Serializable, NetSerializable]
public sealed class IFFConsoleBoundUserInterfaceState : BoundUserInterfaceState
{
    public IFFFlags AllowedFlags { get; init; } // Forge-Change
    public IFFFlags Flags { get; init; } // Forge-Change
    public TimeSpan? HideEndTime { get; init; } // Forge-Change
    public TimeSpan? HideCooldownEndTime { get; init; } // Forge-Change
}

[Serializable, NetSerializable]
public enum IFFConsoleUiKey : byte
{
    Key,
}
