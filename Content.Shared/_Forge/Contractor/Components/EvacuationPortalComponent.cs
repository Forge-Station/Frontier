namespace Content.Shared._Forge.Contractor.Components;

/// <summary>
/// The component responsible for passing the goal on and fulfilling the contract
/// </summary>
[RegisterComponent]
public sealed partial class EvacuationPortalComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public string? LinkedContractId;
}

[ByRefEvent]
public record struct ContractCompletedEvent(ActiveContract Contract);
