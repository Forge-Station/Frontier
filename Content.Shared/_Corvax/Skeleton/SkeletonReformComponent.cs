using Robust.Shared.GameStates;

namespace Content.Shared._Corvax.Skeleton;


[RegisterComponent, NetworkedComponent]
public sealed partial class SkeletonReformComponent : Component
{

    [DataField]
    public string PopupText = "species-reform-default-popup";

    [DataField]
    public bool ShouldStun;

    [DataField]
    public float ReformTime;


    [DataField]
    public string? ActionPrototype;


    [DataField]
    public bool StartDelayed;


    [DataField]
    public EntityUid? OriginalBody;


    [ViewVariables]
    public EntityUid? ActionEntity;
}

