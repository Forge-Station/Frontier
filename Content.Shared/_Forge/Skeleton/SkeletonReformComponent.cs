using Robust.Shared.GameStates;

namespace Content.Shared._Forge.Skeleton;

[RegisterComponent] [NetworkedComponent]
public sealed partial class SkeletonReformComponent : Component
{
    [ViewVariables]
    public EntityUid? ActionEntity;


    [DataField]
    public string? ActionPrototype;


    [DataField]
    public EntityUid? OriginalBody;

    [DataField]
    public string PopupText = "species-reform-default-popup";

    [DataField]
    public float ReformTime;

    [DataField]
    public bool ShouldStun;


    [DataField]
    public bool StartDelayed;
}
