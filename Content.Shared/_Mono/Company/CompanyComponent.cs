using Robust.Shared.GameStates;

namespace Content.Shared._Mono.Company;

/// <summary>
/// Component that represents a player's affiliated company.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CompanyComponent : Component
{
    /// <summary>
    /// The name of the company the player belongs to.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string CompanyName = string.Empty;
}
