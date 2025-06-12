using Content.Server.Polymorph.Systems;
using Content.Shared.Mobs;
using Content.Shared.Polymorph;
using Content.Shared._Corvax;
using Content.Shared._NF.Bank;
using Content.Shared.Actions;
using Content.Shared._NF.Bank.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

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
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

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

        // 💰 Копируем банковский счёт
        if (_entMan.TryGetComponent<BankAccountComponent>(uid, out var bankFrom))
        {
            var bankSystem = _entMan.System<SharedBankSystem>();
            bankSystem.SetBalance(newEntity.Value, bankFrom.Balance);
        }

        // 💬 Копируем имя
        if (_entMan.TryGetComponent(uid, out MetaDataComponent? metaOriginal))
        {
            _meta.SetEntityName(newEntity.Value, metaOriginal.EntityName);
        }

        // 🧠 Mind НЕ переносим (ожидается позже при воскрешении)

        // ✅ Добавляем экшен через корректный API
        if (!string.IsNullOrEmpty(reform.ActionPrototype))
        {
            EnsureComp<ActionsComponent>(newEntity.Value);
            _actionsSystem.AddAction(newEntity.Value, ref reform.ActionEntity, reform.ActionPrototype);

            if (reform.StartDelayed && reform.ReformTime > 0 && reform.ActionEntity != null)
            {
                var now = _timing.CurTime;
                _actionsSystem.SetCooldown(reform.ActionEntity.Value, now, now + TimeSpan.FromSeconds(reform.ReformTime));
            }
        }
    }
}
