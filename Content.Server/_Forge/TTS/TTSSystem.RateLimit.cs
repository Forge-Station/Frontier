using Content.Server.Chat.Managers;
using Content.Server.Players.RateLimiting;
using Content.Shared._Forge;
using Content.Shared.Players.RateLimiting;
using Robust.Shared.Player;

namespace Content.Server._Forge.TTS;

public sealed partial class TTSSystem
{
    [Dependency] private readonly PlayerRateLimitManager _rateLimitManager = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    private const string RateLimitKey = "TTS";

    private void RegisterRateLimits()
    {
        _rateLimitManager.Register(RateLimitKey,
            new RateLimitRegistration(
                ForgeVars.TTSRateLimitPeriod,
                ForgeVars.TTSRateLimitCount,
                RateLimitPlayerLimited)
            );
    }

    private void RateLimitPlayerLimited(ICommonSession player)
    {
        _chat.DispatchServerMessage(player, Loc.GetString("tts-rate-limited"), suppressLog: true);
    }

    private RateLimitStatus HandleRateLimit(ICommonSession player)
    {
        return _rateLimitManager.CountAction(player, RateLimitKey);
    }
}
