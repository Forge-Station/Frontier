using Content.Shared._Forge.Mercenary;
using Robust.Client.GameObjects;

namespace Content.Client._Forge.Mercenary.Systems;

public sealed partial class MercenarySystem : SharedMercenarySystem
{
    [Dependency] private readonly AnimationPlayerSystem _player = default!;

    public override void Initialize()
    {
        base.Initialize();
    }
}
