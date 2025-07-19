using Content.Shared._Forge.Miners;
using Robust.Client.GameObjects;

namespace Content.Client._Forge.Miners.Systems;

public sealed partial class MinersSystem : SharedMinersSystem
{
    [Dependency] private readonly AnimationPlayerSystem _player = default!;

    public override void Initialize()
    {
        base.Initialize();
    }
}
