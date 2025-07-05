using Content.Shared._Forge.Respawn;

namespace Content.Client._Forge.Respawn;

public sealed class RespawnSystem : EntitySystem
{
    public TimeSpan? RespawnResetTime { get; private set; }

    public event Action? RespawnReseted;

    public override void Initialize()
    {
        SubscribeNetworkEvent<RespawnResetEvent>(OnRespawnReset);
    }

    private void OnRespawnReset(RespawnResetEvent e)
    {
        RespawnResetTime = e.Time;

        RespawnReseted?.Invoke();
    }
}
