using Robust.Shared.GameStates;

namespace Content.Shared._Corvax.Skeleton;

[RegisterComponent, NetworkedComponent]
public sealed partial class SkeletonSkullComponent : Component
{
    [DataField("originalBody")]
    public EntityUid? OriginalBody = default;
}
