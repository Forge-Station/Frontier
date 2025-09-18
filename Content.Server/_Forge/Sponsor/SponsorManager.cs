using System.Diagnostics.CodeAnalysis;
using Content.Shared._Forge.Sponsors;
using JetBrains.Annotations;
using Robust.Shared.Network;

namespace Content.Server._Forge.Sponsors;

[UsedImplicitly]
public sealed class SponsorManager : ISharedSponsorManager
{
    public void Initialize() { }
    public Dictionary<NetUserId, SponsorLevel> Sponsors = new();
    public bool TryGetSponsor(NetUserId user, [NotNullWhen(true)] out SponsorLevel level)
    {
        return Sponsors.TryGetValue(user, out level);
    }

    public bool TryGetSponsorColor(SponsorLevel level, [NotNullWhen(true)] out string? color)
    {
        return SponsorData.SponsorColor.TryGetValue(level, out color);
    }

    public bool TryGetSponsorGhost(SponsorLevel level, [NotNullWhen(true)] out string? ghost)
    {
        return SponsorData.SponsorGhost.TryGetValue(level, out ghost);
    }
}
