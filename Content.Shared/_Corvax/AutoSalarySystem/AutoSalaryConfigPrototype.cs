using Robust.Shared.Prototypes;

namespace Content.Shared._Corvax.AutoSalarySystem;

[Prototype("autoSalaryConfig")]
public sealed class AutoSalaryConfigPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    [DataField("payInterval", required: true)]
    public float PayIntervalSeconds { get; private set; }
}
