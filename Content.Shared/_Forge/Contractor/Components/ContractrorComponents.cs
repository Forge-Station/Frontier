namespace Content.Shared._Forge.Contractor.Components;

/// <summary>
/// Just a stub for contactor logic
/// </summary>
[RegisterComponent]
public sealed partial class ContractorComponent : Component
{
    /// <summary>
    /// Counter of completed contracts for the end of the round
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int CountContracts = 0;

    /// <summary>
    /// The field for storing the user's uplink
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? Uplink = default!;
}
