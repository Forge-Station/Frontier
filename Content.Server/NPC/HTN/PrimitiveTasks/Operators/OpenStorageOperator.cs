using Content.Server.Storage.Components;
using Content.Shared.Interaction;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Opens a storage container.
/// </summary>
public sealed partial class OpenStorageOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private OpenableSystem _openable = default!;
    private SharedInteractionSystem _interaction = default!;

    [DataField("targetKey")]
    public string TargetKey = "Target";

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _openable = sysManager.GetEntitySystem<OpenableSystem>();
        _interaction = sysManager.GetEntitySystem<SharedInteractionSystem>();
    }

    public override HTNOperatorStatus Update(NPCBlackboard blackboard, float frameTime)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityUid>(TargetKey, out var target, _entManager))
        {
            return HTNOperatorStatus.Failed;
        }

        if (!_entManager.HasComponent<EntityStorageComponent>(target))
        {
            return HTNOperatorStatus.Failed;
        }

        if (!_openable.IsClosed(target))
        {
            return HTNOperatorStatus.Finished;
        }

        if (!_interaction.InRangeUnobstructed(owner, target, range: SharedInteractionSystem.InteractionRange - 0.1f))
        {
            return HTNOperatorStatus.Failed;
        }

        // This will handle most checks like welded, locked, etc. on the server-side.
        // It also plays sounds and animations.
        _interaction.InteractionActivate(owner, target);

        // We don't know if it succeeded for sure (e.g. it might be locked and we don't have access),
        // but we'll assume it did for now. The HTN will replan if the container is still closed
        // during the next evaluation of the IdleCompound task.
        // PickNearbyStorageOperator already filters out locked containers, so this should be fine.
        return HTNOperatorStatus.Finished;
    }
}
