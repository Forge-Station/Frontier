using Content.Server.NPC;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Content.Shared.Physics;
using Robust.Shared.ViewVariables;

namespace Content.Server.Npc.Components
{
    [RegisterComponent]
    public sealed partial class NPCPerceptionComponent : Component
    {
        [DataField("visionRadius"), ViewVariables(VVAccess.ReadWrite)]
        public float VisionRadius = 8f;

        [ViewVariables(VVAccess.ReadWrite)]
        public float ScanAccumulator = 0f;

        [DataField("scanFrequency"), ViewVariables(VVAccess.ReadWrite)]
        public float ScanFrequency = 1.0f;

        /// <summary>
        /// Where to store the target.
        /// </summary>
        [DataField("targetKey")]
        public string TargetKey = NPCBlackboard.UtilityTarget;

        /// <summary>
        /// Collision groups to consider for line of sight.
        /// </summary>
        [DataField("visionMask"), ViewVariables(VVAccess.ReadWrite)]
        public CollisionGroup VisionMask = CollisionGroup.Impassable;
    }
}
