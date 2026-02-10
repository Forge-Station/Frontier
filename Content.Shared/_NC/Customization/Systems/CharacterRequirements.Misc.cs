using System.Diagnostics.CodeAnalysis;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Shared.Customization.Systems;

/// <summary>
/// Requires the server to have at least a minimum number of players.
/// </summary>
[UsedImplicitly]
[Serializable, NetSerializable]
public sealed partial class MinPlayersRequirement : JobRequirement
{
    [DataField(required: true)]
    public int Min;

    public override bool Check(IEntityManager entManager,
        IPrototypeManager protoManager,
        HumanoidCharacterProfile? profile,
        IReadOnlyDictionary<string, TimeSpan> playTimes,
        [NotNullWhen(false)] out FormattedMessage? reason)
    {
        var playerManager = IoCManager.Resolve<ISharedPlayerManager>();
        var playerCount = playerManager.PlayerCount;

        // CVar-флаг, чтобы можно было включать/выключать проверку
        var configManager = IoCManager.Resolve<IConfigurationManager>();
        var enabled = configManager.GetCVar(CCVars.MinPlayersRequirement);

        reason = FormattedMessage.FromMarkupPermissive(
            Loc.GetString("character-minPlayers-requirement", ("min", Min)));

        if (!enabled)
            return true;

        if (!Inverted)
        {
            if (playerCount < Min)
                return false;
        }
        else
        {
            if (playerCount >= Min)
                return false;
        }

        return true;
    }
}
