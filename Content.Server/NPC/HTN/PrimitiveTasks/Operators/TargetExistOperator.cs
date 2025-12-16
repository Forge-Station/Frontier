using Content.Server.NPC; // Нужно для NPCBlackboard
using Content.Server.Npc.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.IoC; // Для Dependency

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators
{
    public sealed partial class TargetExistOperator : HTNOperator
    {
        // Внедряем зависимость EntityManager
        [Dependency] private readonly IEntityManager _entityManager = default!;

        // Ключ в Blackboard, куда мы запишем цель
        [DataField("targetKey")]
        public string TargetKey = "Target";

        // Ключ Owner (владельца) обычно константа в NPCBlackboard
        [DataField("ownerKey")]
        public string OwnerKey = NPCBlackboard.Owner;

        public override void Initialize(IEntitySystemManager sysManager)
        {
            base.Initialize(sysManager);
            // Зависимости инжектятся автоматически при создании оператора, 
            // но в некоторых версиях может потребоваться ручная инициализация, 
            // однако для [Dependency] в HTNOperator обычно это работает само.
        }

        // В новых версиях метод называется Update, а не Task
        public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
        {
            // Получаем владельца (НПС)
            if (!blackboard.TryGetValue<EntityUid>(OwnerKey, out var owner, _entityManager))
            {
                return HTNOperatorStatus.Failed;
            }

            // Пытаемся достать наш компонент SpaceNpcComponent
            if (!_entityManager.TryGetComponent<SpaceNpcComponent>(owner, out var npcComp))
            {
                // Если компонента нет - фейл задачи
                return HTNOperatorStatus.Failed;
            }

            // Проверяем, есть ли цель и жива ли она
            if (npcComp.CurrentTarget != null && _entityManager.EntityExists(npcComp.CurrentTarget))
            {
                // Записываем цель в память HTN
                blackboard.SetValue(TargetKey, npcComp.CurrentTarget.Value);

                // Возвращаем Finished, так как задача поиска выполнена успешно (цель есть)
                return HTNOperatorStatus.Finished;
            }

            // Если цели нет -> Failed
            return HTNOperatorStatus.Failed;
        }
    }
}
