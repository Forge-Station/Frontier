namespace Content.Shared._Forge.Contractor.Components;

/// <summary>
/// The component responsible for correctly calling the evacuation portal in the contractor's logic
/// </summary>
[RegisterComponent]
public sealed partial class FlareEvacuationComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public bool Activate = false;

    [DataField]
    public TimeSpan UpdateTime = TimeSpan.Zero;

    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string? LinkedContractId;
}
