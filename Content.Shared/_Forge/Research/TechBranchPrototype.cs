using Robust.Shared.Prototypes;

namespace Content.Shared.Research.Prototypes;

[Prototype("techBranch")]
public sealed class TechBranchPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField("uiName")]
    public string UiName { get; private set; } = default!;
}
