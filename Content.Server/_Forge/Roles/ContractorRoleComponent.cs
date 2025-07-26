using Content.Shared.Roles;

namespace Content.Server.Roles;

/// <summary>
/// Added to mind role entities to tag that they are a contractor.
/// </summary>
[RegisterComponent]
public sealed partial class ContractorRoleComponent : BaseMindRoleComponent
{
}
