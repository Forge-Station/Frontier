using System.Threading.Tasks;
using Content.Server.Database;
using Robust.Shared.Network;

namespace Content.Server.Connection.Whitelist.Conditions;

/// <summary>
/// Condition that matches if the player is in the sponsors.
/// </summary>
public sealed partial class ConditionManualSponsorMembership : WhitelistCondition
{
}
