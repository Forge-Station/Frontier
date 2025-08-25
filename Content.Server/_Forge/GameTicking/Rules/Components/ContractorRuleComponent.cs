namespace Content.Server.GameTicking.Rules.Components;

/// <summary>
/// Stores data for <see cref="ContractorRuleSystem"/>.
/// </summary>
[RegisterComponent, Access(typeof(ContractorRuleSystem))]
public sealed partial class ContractorRuleComponent : Component
{
    /// <summary>
    /// It stores an initially initialized list of suitable candidates.
    /// </summary>
    public List<EntityUid> SelectedCandidates = new();

    /// <summary>
    /// Waiting time before selecting candidates (in minutes).
    /// </summary>
    public float Duration = 1f;
}
