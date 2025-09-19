using System.Diagnostics.CodeAnalysis;
using Content.Shared._Forge.Sponsors;
using JetBrains.Annotations;
using Robust.Shared.Network;

namespace Content.Client._Forge.Sponsors;

[UsedImplicitly]
public sealed class SponsorManager : ISharedSponsorManager
{
    [Dependency] private readonly IClientNetManager _netMgr = default!;
    private Dictionary<NetUserId, SponsorLevel> _sponsors = new();
    public void Initialize()
    {
        _netMgr.RegisterNetMessage<MsgSyncSponsorData>(OnSponsorDataReceived);
    }

    private void OnSponsorDataReceived(MsgSyncSponsorData message)
    {
        if (_sponsors.ContainsKey(message.UserId))
            _sponsors[message.UserId] = message.Level;

        else
            _sponsors.Add(message.UserId, message.Level);
    }

    public bool TryGetSponsor(NetUserId user, out SponsorLevel level)
    {
        return _sponsors.TryGetValue(user, out level);
    }
}
