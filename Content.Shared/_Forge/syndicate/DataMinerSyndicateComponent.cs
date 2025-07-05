using Content.Shared.Tools;

namespace Content.Shared._Forge.DataMiner.Components;

/// <summary>
/// The component responsible for the data miners
/// </summary>
[RegisterComponent]
public sealed partial class DataMinerSyndicateComponent : Component
{
    /// <summary>
    /// The miner's balance is correct
    /// </summary>
    [DataField, ViewVariables(VVAccess.ReadOnly)]
    public int Balance = 0;

    /// <summary>
    /// The amount received each time depends on the tick
    /// </summary>
    [DataField]
    public int AmountAccruals = 90;

    /// <summary>
    /// The time after which the payment will be made again
    /// </summary>
    [DataField]
    public float Cooldown = 10f;

    /// <summary>
    /// Timer field
    /// </summary>
    [DataField]
    public float NextTimeTick { get; set; } = 10f;

    /// <summary>
    /// The type of balance extraction tool
    /// </summary>
    [ValidatePrototypeId<ToolQualityPrototype>]
    public string ExtractQuality = "Pulsing";
}
