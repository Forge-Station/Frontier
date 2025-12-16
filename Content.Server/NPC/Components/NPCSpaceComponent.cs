using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.ViewVariables;

namespace Content.Server.Npc.Components
{
    [RegisterComponent]
    public sealed partial class SpaceNpcComponent : Component // Добавьте partial, это стандарт для компонентов
    {
        // ВАЖНО: Это должно быть public float, без { get; set; } и без Func<>
        [DataField("visionRadius"), ViewVariables(VVAccess.ReadWrite)]
        public float VisionRadius = 30f;

        [ViewVariables]
        public EntityUid? CurrentTarget = null;

        [ViewVariables(VVAccess.ReadWrite)]
        public float ScanAccumulator = 0f;

        [DataField("scanFrequency"), ViewVariables(VVAccess.ReadWrite)]
        public float ScanFrequency = 1.0f;
    }
}
