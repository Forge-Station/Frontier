using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Corvax
{
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
        public ProtoId<EntityPrototype>? ReformPrototype;

        [ViewVariables]
        public EntityUid? ActionEntity;

        [DataField]
        public EntityUid? OriginalBody;
    }
}
