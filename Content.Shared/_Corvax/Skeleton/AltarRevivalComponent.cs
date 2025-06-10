using Robust.Shared.GameStates;

namespace Content.Shared._Corvax.Skeleton
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class AltarRevivalComponent : Component
    {
        [DataField] public string SkullPrototype = "SkeletonSkull";
        [DataField] public string RequiredItemPrototype = "CapCoin";
        [DataField] public int RequiredItemCount = 10;
    }
}
