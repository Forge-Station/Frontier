using Robust.Shared.Serialization;

namespace Content.Shared._NF.Shipyard.Events;

/// <summary>
///     Rename a ship registered to the deed on the ID card
/// </summary>
[Serializable, NetSerializable]
public sealed class ShipyardConsoleRenameMessage : BoundUserInterfaceMessage
{
    public string NewName;

    public ShipyardConsoleRenameMessage(string newName)
    {
        NewName = newName;
    }
}
