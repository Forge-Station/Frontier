using Robust.Shared.Prototypes;

namespace Content.Shared._Corvax.AutoSalarySystem;

[Prototype("autoSalaryJob")]
public sealed class AutoSalaryJobPrototype : IPrototype
{
    [IdDataField] public string ID { get; } = default!;

    [DataField("salary", required: true)]
    public int Salary { get; private set; }
}
