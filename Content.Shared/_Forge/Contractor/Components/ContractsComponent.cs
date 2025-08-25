using Content.Shared._Forge.Contractor;

namespace Content.Shared._Forge.Contracts;

[RegisterComponent]
public sealed partial class ContractsComponent : Component
{
    /// <summary>
    /// The storage field for the uplink owner
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public EntityUid? UplinkOwner = default!;

    /// <summary>
    /// List of available contract IDs
    /// </summary>
    [DataField("availableContracts"), ViewVariables(VVAccess.ReadOnly)]
    public List<ContractData> AvailableContracts = new();

    /// <summary>
    /// Active Player Contracts
    /// </summary>
    [DataField("activeContracts"), ViewVariables(VVAccess.ReadOnly)]
    public List<ActiveContract> ActiveContracts = new();

    /// <summary>
    /// Time for the next contract update
    /// </summary>
    [DataField("nextUpdateTime")]
    public TimeSpan NextUpdateTime = TimeSpan.Zero;

    /// <summary>
    /// Contract renewal interval
    /// </summary>
    [DataField("updateInterval")]
    public TimeSpan UpdateInterval = TimeSpan.FromMinutes(20);
}
