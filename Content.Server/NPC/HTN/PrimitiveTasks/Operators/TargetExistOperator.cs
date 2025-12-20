using Content.Server.NPC;
using Content.Server.Npc.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.IoC;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators
{
    public sealed partial class TargetExistOperator : HTNOperator
    {
        [Dependency] private readonly IEntityManager _entityManager = default!;


        [DataField("targetKey")]
        public string TargetKey = "Target";

        [DataField("ownerKey")]
        public string OwnerKey = NPCBlackboard.Owner;

        public override void Initialize(IEntitySystemManager sysManager)
        {
            base.Initialize(sysManager);
        }

        public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
        {
            if (!blackboard.TryGetValue<EntityUid>(OwnerKey, out var owner, _entityManager))
            {
                return HTNOperatorStatus.Failed;
            }

            if (!_entityManager.TryGetComponent<SpaceNpcComponent>(owner, out var npcComp))
            {
                return HTNOperatorStatus.Failed;
            }

            if (npcComp.CurrentTarget != null && _entityManager.EntityExists(npcComp.CurrentTarget))
            {
                blackboard.SetValue(TargetKey, npcComp.CurrentTarget.Value);

                return HTNOperatorStatus.Finished;
            }

            // Если цели нет -> Failed
            return HTNOperatorStatus.Failed;
        }
    }
}
