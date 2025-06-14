using Content.Shared.Corvax.Mercenary;
using Robust.Client.GameObjects;

namespace Content.Client.Corvax.Mercenary.Systems;

public sealed partial class MercenarySystem : SharedMercenarySystem
{
    [Dependency] private readonly AnimationPlayerSystem _player = default!;

    public override void Initialize()
    {
        base.Initialize();
    }
}
