using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared._Corvax;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Content.Shared.Actions;
using Robust.Shared.Timing;

namespace Content.Server._Corvax;

public sealed partial class SkeletonReformSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = null!;
    [Dependency] private readonly MindSystem _mindSystem = null!;
    [Dependency] private readonly DamageableSystem _damageable = null!;
    [Dependency] private readonly PopupSystem _popup = null!;
    [Dependency] private readonly MobStateSystem _mobState = null!;
    [Dependency] private readonly IGameTiming _timing = null!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = null!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SkeletonReformComponent, ReformEvent>(OnReform);
        SubscribeLocalEvent<SkeletonReformComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, SkeletonReformComponent comp, ComponentStartup args)
    {
        // Добавляем экшен через правильный API
        if (!string.IsNullOrEmpty(comp.ActionPrototype))
        {
            EnsureComp<ActionsComponent>(uid);
            _actionsSystem.AddAction(uid, ref comp.ActionEntity, comp.ActionPrototype);

            if (comp.StartDelayed && comp.ReformTime > 0 && comp.ActionEntity != null)
            {
                var now = _timing.CurTime;
                _actionsSystem.SetCooldown(comp.ActionEntity.Value, now, now + TimeSpan.FromSeconds(comp.ReformTime));
            }
        }
    }

    private void OnReform(EntityUid skullUid, SkeletonReformComponent comp, ReformEvent args)
    {
        if (comp.OriginalBody is not { } bodyUid || !_entMan.EntityExists(bodyUid))
        {
            _popup.PopupEntity("Невозможно найти тело скелета.", skullUid, args.User);
            return;
        }

        if (!_entMan.TryGetComponent<MobStateComponent>(bodyUid, out _))
        {
            _popup.PopupEntity("Тело скелета недействительно.", skullUid, args.User);
            return;
        }

        // Восстанавливаем здоровье
        if (_entMan.TryGetComponent<DamageableComponent>(bodyUid, out var damageable))
        {
            _damageable.SetAllDamage(bodyUid, damageable, FixedPoint2.Zero);
        }

        // Восстанавливаем MobState
        _mobState.ChangeMobState(bodyUid, MobState.Alive);

        // Восстанавливаем разум
        if (_mindSystem.TryGetMind(skullUid, out var mindId, out var mind))
        {
            _mindSystem.TransferTo(mindId, bodyUid, mind: mind);
        }

        // Восстанавливаем банковский счёт
        if (_entMan.TryGetComponent<BankAccountComponent>(skullUid, out var bankFrom))
        {
            var bankSys = _entMan.System<SharedBankSystem>();
            bankSys.SetBalance(bodyUid, bankFrom.Balance);
        }

        // Показываем всплывающее сообщение
        var popupText = string.IsNullOrEmpty(comp.PopupText)
            ? "Скелет восстал из мёртвых!"
            : Loc.GetString(comp.PopupText, ("name", bodyUid));
        _popup.PopupEntity(popupText, bodyUid, args.User);

        // Удаляем череп
        QueueDel(skullUid);
    }

    public sealed partial class ReformEvent : InstantActionEvent
    {
        public EntityUid User;
        public ReformEvent(EntityUid user) => User = user;
    }
}
