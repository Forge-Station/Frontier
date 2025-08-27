using Content.Shared._ADT.Turrets.Components;
using Content.Shared._ADT.Turrets.Events;
using Content.Shared.Actions;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Destructible;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.MouseRotator;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._ADT.Turrets.Systems;

/// <summary>
/// Управление контролируемыми турелями/дронами:
/// - Пересадка Mind в цель и назад
/// - Экшен возврата в тело
/// - Ограничения по состояниям тела
/// - Режим дрона: временно навешивает компоненты для движения/поворота мышью
/// </summary>
public sealed class TurretControllableSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;

    private readonly HashSet<EntityUid> _addedMover = new();
    private readonly HashSet<EntityUid> _addedRotator = new();

    private readonly Dictionary<EntityUid, EntityUid> _bodyToTurret = new();
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly StatusEffectsSystem _statusNew = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TurretControllableComponent, MapInitEvent>(OnStartup);
        SubscribeLocalEvent<TurretControllableComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TurretControllableComponent, DestructionEventArgs>(OnDestruction);

        SubscribeLocalEvent<TurretControllableComponent, ControlReturnActionEvent>(OnReturn);
        SubscribeLocalEvent<TurretControllableComponent, GettingControlledEvent>(OnGettingControlled);

        SubscribeLocalEvent<TurretControllableComponent, MoveInputEvent>(OnUserMoveInput);

        SubscribeLocalEvent<DamageableComponent, DamageChangedEvent>(OnBodyDamaged);
    }

    private void OnStartup(EntityUid uid, TurretControllableComponent component, MapInitEvent args)
    {
        _actionsSystem.AddAction(uid, ref component.ControlReturnActEntity, component.ControlReturnAction);
    }

    private void OnShutdown(EntityUid uid, TurretControllableComponent component, ComponentShutdown args)
    {
        Return(uid, component);
        _actionsSystem.RemoveAction(component.ControlReturnActEntity);
    }

    private void OnDestruction(EntityUid uid, TurretControllableComponent component, DestructionEventArgs args)
    {
        Return(uid, component);
        _actionsSystem.RemoveAction(component.ControlReturnActEntity);
    }

    public void OnGettingControlled(EntityUid uid, TurretControllableComponent comp, GettingControlledEvent e)
    {
        comp.User = e.User;
        comp.Controller = e.Controller;
        _bodyToTurret[e.User] = uid;

        if (comp.IsMoveable || comp.IsDrone)
            EnsureMovementStack(uid, comp);
    }

    public void OnReturn(EntityUid uid, TurretControllableComponent component, ControlReturnActionEvent args)
    {
        Return(uid, component);
    }

    private void OnUserMoveInput(Entity<TurretControllableComponent> turret, ref MoveInputEvent args)
    {
        if (!args.HasDirectionalMovement)
            return;

        if (!turret.Comp.IsMoveable && !turret.Comp.IsDrone)
            Return(args.Entity, turret.Comp);
    }

    private void OnBodyDamaged(EntityUid uid, DamageableComponent _, ref DamageChangedEvent args)
    {
        if (_bodyToTurret.TryGetValue(uid, out var turretUid) &&
            TryComp<TurretControllableComponent>(turretUid, out var tComp) &&
            tComp.User == uid)
            Return(turretUid, tComp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var it = EntityQueryEnumerator<TurretControllableComponent>();
        while (it.MoveNext(out var uid, out var comp))
        {
            if (comp.User is not { } user || comp.Controller is null)
                continue;

            var shouldReturn =
                !_mobStateSystem.IsAlive(user) ||
                HasComp<SleepingComponent>(user) ||
                _statusNew.HasEffectComp<ForcedSleepingStatusEffectComponent>(user);

            if (shouldReturn)
                Return(uid, comp);
        }
    }

    private void Return(EntityUid uid, TurretControllableComponent comp)
    {
        if (comp.User is { } body)
            _bodyToTurret.Remove(body);

        if (TryComp<MindContainerComponent>(uid, out var mind) && mind.HasMind)
            TryReturnToBody(uid, comp);

        comp.User = null;

        if (comp.Controller is not null)
        {
            RaiseLocalEvent((EntityUid)comp.Controller, new ReturnToBodyTurretEvent(uid));
            comp.Controller = null;
        }

        TeardownMovementStack(uid);
    }

    public bool TryReturnToBody(EntityUid uid, TurretControllableComponent component)
    {
        if (component.User is not null)
        {
            _mindSystem.ControlMob(uid, (EntityUid)component.User);
            return true;
        }

        return false;
    }

    private void EnsureMovementStack(EntityUid uid, TurretControllableComponent comp)
    {
        if (!HasComp<InputMoverComponent>(uid))
        {
            AddComp<InputMoverComponent>(uid);
            _addedMover.Add(uid);
        }

        if (comp.UseMouseRotation && !HasComp<MouseRotatorComponent>(uid))
        {
            AddComp<MouseRotatorComponent>(uid);
            _addedRotator.Add(uid);
        }
    }

    private void TeardownMovementStack(EntityUid uid)
    {
        if (_addedMover.Remove(uid) && TryComp<InputMoverComponent>(uid, out _))
            RemComp<InputMoverComponent>(uid);

        if (_addedRotator.Remove(uid) && TryComp<MouseRotatorComponent>(uid, out _))
            RemComp<MouseRotatorComponent>(uid);
    }
}
