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

            if (!_entityManager.TryGetComponent<NPCPerceptionComponent>(owner, out var perceptionComp))
            {
                return HTNOperatorStatus.Failed;
            }

            if (blackboard.TryGetValue<EntityUid>(perceptionComp.TargetKey, out var target, _entityManager) && _entityManager.EntityExists(target))
            {
                blackboard.SetValue(TargetKey, target);

                return HTNOperatorStatus.Finished;
            }

            // Если цели нет -> Failed
            return HTNOperatorStatus.Failed;
        }
    }
}
