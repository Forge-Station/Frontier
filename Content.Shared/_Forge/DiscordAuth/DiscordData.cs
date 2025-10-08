using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._Forge.DiscordAuth;

[Serializable, NetSerializable]
public sealed class DiscordData
{
    public bool Status { get; set; }
    public string? ErrorMessage { get; set; }

    public DiscordUserData? UserData { get; set; }

    public DiscordData(bool status, DiscordUserData? userData, string? errMsg = null)
    {
        Status = status;
        UserData = userData;
        ErrorMessage = errMsg;
    }
}

[Serializable, NetSerializable]
public sealed class DiscordUserData(NetUserId userId, string discordId)
{
    public NetUserId UserId { get; set; } = userId;
    public string DiscordId { get; set; } = discordId;

}
