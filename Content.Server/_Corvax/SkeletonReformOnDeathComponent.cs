using Content.Server.Mind;
using Content.Server.Polymorph.Systems;
using Content.Shared.Mobs;
using Content.Shared.Polymorph;
using Content.Shared._Corvax;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Server._Corvax;

[RegisterComponent]
public sealed partial class SkeletonReformOnDeathComponent : Component
{
    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> PolymorphId;
}

public sealed class SkeletonReformOnDeathSystem : EntitySystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SkeletonReformOnDeathComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(EntityUid uid, SkeletonReformOnDeathComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        Logger.Info($"[SkeletonReform] Mob {ToPrettyString(uid)} died, attempting to spawn skull.");

        // Превращаем тело в череп, но не удаляем его
        var newEntity = _polymorph.PolymorphEntity(uid, component.PolymorphId);
        if (newEntity == null)
        {
            Logger.Warning($"[SkeletonReform] Polymorph failed for {ToPrettyString(uid)}.");
            return;
        }

        if (!_entMan.TryGetComponent(newEntity.Value, out SkeletonReformComponent? reform))
        {
            Logger.Warning($"[SkeletonReform] SkeletonReformComponent missing on skull {ToPrettyString(newEntity.Value)}.");
            return;
        }

        reform.OriginalBody = uid;

        Logger.Info($"[SkeletonReform] Skull {ToPrettyString(newEntity.Value)} linked to body {ToPrettyString(uid)}");

        // Добавление экшена, если указан в компоненте
        if (!string.IsNullOrEmpty(reform.ActionPrototype))
        {
            var actionEntity = _entMan.SpawnEntity(reform.ActionPrototype, Transform(newEntity.Value).Coordinates);
            reform.ActionEntity = actionEntity;

            if (_entMan.TryGetComponent(newEntity.Value, out ActionsComponent? actions))
            {
                var actionSys = _entMan.System<SharedActionsSystem>();
                actionSys.AddAction(newEntity.Value, actionEntity, actionEntity, actions);
            }
            else
            {
                Logger.Warning($"[SkeletonReform] Skull {ToPrettyString(newEntity.Value)} has no ActionsComponent.");
            }
        }

        // ⚠ Mind НЕ переносим
        // ⚠ Тело НЕ удаляем — сохраняется для воскрешения
    }

}
