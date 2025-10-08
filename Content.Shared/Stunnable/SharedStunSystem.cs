using Content.Shared.ActionBlocker;
using Content.Shared.Administration.Logs;
using Content.Shared.Alert; // Forge-Change
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems; // Forge-Change
using Content.Shared.Bed.Sleep;
using Content.Shared.Database;
using Content.Shared.DoAfter; // Forge-Change
using Content.Shared.Hands;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Standing;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing; // Forge-Change

namespace Content.Shared.Stunnable;

public abstract partial class SharedStunSystem : EntitySystem // Forge-Change (patrial)
{
    [Dependency] protected readonly ActionBlockerSystem Blocker = default!; // Forge-Change
    [Dependency] protected readonly AlertsSystem Alerts = default!; // Forge-Change
    [Dependency] protected readonly IGameTiming GameTiming = default!; // Forge-Change
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly EntityWhitelistSystem _entityWhitelist = default!; // Forge-Change
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!; // Forge-Change
    [Dependency] protected readonly SharedDoAfterSystem DoAfter = default!; // Forge-Change
    [Dependency] protected readonly SharedStaminaSystem Stamina = default!; // Forge-Change
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;

    /// <summary>
    /// Friction modifier for knocked down players.
    /// Doesn't make them faster but makes them slow down... slower.
    /// </summary>
    public const float KnockDownModifier = 0.2f;

    public override void Initialize()
    {
        SubscribeLocalEvent<SlowedDownComponent, ComponentInit>(OnSlowInit);
        SubscribeLocalEvent<SlowedDownComponent, ComponentShutdown>(OnSlowRemove);
        SubscribeLocalEvent<SlowedDownComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed); // Forge-Change

        SubscribeLocalEvent<StunnedComponent, ComponentStartup>(UpdateCanMove);
        SubscribeLocalEvent<StunnedComponent, ComponentShutdown>(OnStunShutdown); // Forge-Change

        SubscribeLocalEvent<StunOnContactComponent, StartCollideEvent>(OnStunOnContactCollide);

        // Attempt event subscriptions.
        SubscribeLocalEvent<StunnedComponent, ChangeDirectionAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, UpdateCanMoveEvent>(OnMoveAttempt);
        SubscribeLocalEvent<StunnedComponent, InteractionAttemptEvent>(OnAttemptInteract);
        SubscribeLocalEvent<StunnedComponent, UseAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, ThrowAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, DropAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, AttackAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, PickupAttemptEvent>(OnAttempt);
        SubscribeLocalEvent<StunnedComponent, IsEquippingAttemptEvent>(OnEquipAttempt);
        SubscribeLocalEvent<StunnedComponent, IsUnequippingAttemptEvent>(OnUnequipAttempt);
        SubscribeLocalEvent<MobStateComponent, MobStateChangedEvent>(OnMobStateChanged);

        InitializeKnockdown(); // Forge-Change
        InitializeAppearance(); // Forge-Change
    }

    private void OnAttemptInteract(Entity<StunnedComponent> ent, ref InteractionAttemptEvent args)
    {
        args.Cancelled = true;
    }

    private void OnMobStateChanged(EntityUid uid, MobStateComponent component, MobStateChangedEvent args)
    {
        if (!TryComp<StatusEffectsComponent>(uid, out var status))
        {
            return;
        }
        switch (args.NewMobState)
        {
            case MobState.Alive:
                {
                    break;
                }
            case MobState.Critical:
                {
                    _statusEffect.TryRemoveStatusEffect(uid, "Stun");
                    break;
                }
            case MobState.Dead:
                {
                    _statusEffect.TryRemoveStatusEffect(uid, "Stun");
                    break;
                }
            case MobState.Invalid:
            default:
                return;
        }

    }
    // Forge-Change-Start
    private void OnStunShutdown(Entity<StunnedComponent> ent, ref ComponentShutdown args)
    {
        // This exists so the client can end their funny animation if they're playing one.
        UpdateCanMove(ent, ent.Comp, args);
        Appearance.RemoveData(ent, StunVisuals.SeeingStars);
    }

    private void UpdateCanMove(EntityUid uid, StunnedComponent component, EntityEventArgs args)
    {
        Blocker.UpdateCanMove(uid);
    }
    // Forge-Change-End
    private void OnStunOnContactCollide(Entity<StunOnContactComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId)
            return;

        if (_entityWhitelist.IsBlacklistPass(ent.Comp.Blacklist, args.OtherEntity))
            return;

        if (!TryComp<StatusEffectsComponent>(args.OtherEntity, out var status))
            return;

        TryStun(args.OtherEntity, ent.Comp.Duration, true, status);
        TryKnockdown(args.OtherEntity, ent.Comp.Duration, ent.Comp.Refresh, ent.Comp.AutoStand); // Forge-Change
    }

    private void OnSlowInit(EntityUid uid, SlowedDownComponent component, ComponentInit args)
    {
        _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
    }

    private void OnSlowRemove(EntityUid uid, SlowedDownComponent component, ComponentShutdown args)
    {
        component.SprintSpeedModifier = 1f;
        component.WalkSpeedModifier = 1f;
        _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
    }

    // TODO STUN: Make events for different things. (Getting modifiers, attempt events, informative events...)

    /// <summary>
    ///     Stuns the entity, disallowing it from doing many interactions temporarily.
    /// </summary>
    public bool TryStun(EntityUid uid, TimeSpan time, bool refresh, StatusEffectsComponent? status = null) // Forge-Change
    {
        if (time <= TimeSpan.Zero)
            return false;

        if (!Resolve(uid, ref status, false))
            return false;

        if (!_statusEffect.TryAddStatusEffect<StunnedComponent>(uid, "Stun", time, refresh))
            return false;

        var ev = new StunnedEvent();
        RaiseLocalEvent(uid, ref ev);

        _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(uid):user} stunned for {time.Seconds} seconds");
        return true;
    }

    /// <summary>
    ///     Knocks down the entity, making it fall to the ground.
    /// </summary>
    public bool TryKnockdown(EntityUid uid, TimeSpan time, bool refresh, bool autoStand = true, bool drop = true) // Forge-Change
    {
        if (time <= TimeSpan.Zero)
            return false;

        // Can't fall down if you can't actually be downed.
        if (!HasComp<StandingStateComponent>(uid)) // Forge-Change
            return false;

        var evAttempt = new KnockDownAttemptEvent(autoStand, drop); // Forge-Change
        RaiseLocalEvent(uid, ref evAttempt); // Forge-Change

        if (evAttempt.Cancelled) // Forge-Change
            return false;
        // Forge-Change-Start
        // Initialize our component with the relevant data we need if we don't have it
        if (EnsureComp<KnockedDownComponent>(uid, out var component))
        {
            RefreshKnockedMovement((uid, component));
            CancelKnockdownDoAfter((uid, component));
        }
        else
        {
            // Only drop items the first time we want to fall...
            if (drop)
            {
                var ev = new DropHandItemsEvent();
                RaiseLocalEvent(uid, ref ev);
            }

            // Only update Autostand value if it's our first time being knocked down...
            SetAutoStand((uid, component), evAttempt.AutoStand);
        }

        var knockedEv = new KnockedDownEvent(time);
        RaiseLocalEvent(uid, ref knockedEv);

        UpdateKnockdownTime((uid, component), knockedEv.Time, refresh);

        Alerts.ShowAlert(uid, KnockdownAlert, null, (GameTiming.CurTime, component.NextUpdate));

        _adminLogger.Add(LogType.Stamina, LogImpact.Medium, $"{ToPrettyString(uid):user} knocked down for {time.Seconds} seconds");
        // Forge-Change-End
        return true;
    }

    /// <summary>
    ///     Applies knockdown and stun to the entity temporarily.
    /// </summary>
    public bool TryParalyze(EntityUid uid, TimeSpan time, bool refresh,
        StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false))
            return false;

        return TryKnockdown(uid, time, refresh) && TryStun(uid, time, refresh, status); // Forge-Change
    }

    /// <summary>
    ///     Slows down the mob's walking/running speed temporarily
    /// </summary>
    public bool TrySlowdown(EntityUid uid, TimeSpan time, bool refresh,
        float walkSpeedMod = 1f, float sprintSpeedMod = 1f, // Forge-Change
        StatusEffectsComponent? status = null)
    {
        if (!Resolve(uid, ref status, false))
            return false;

        if (time <= TimeSpan.Zero)
            return false;

        if (_statusEffect.TryAddStatusEffect<SlowedDownComponent>(uid, "SlowedDown", time, refresh, status))
        {
            var slowed = Comp<SlowedDownComponent>(uid);
            // Doesn't make much sense to have the "TrySlowdown" method speed up entities now does it?
            walkSpeedMod = Math.Clamp(walkSpeedMod, 0f, 1f); // Forge-Change
            sprintSpeedMod = Math.Clamp(sprintSpeedMod, 0f, 1f); // Forge-Change

            slowed.WalkSpeedModifier *= walkSpeedMod; // Forge-Change
            slowed.SprintSpeedModifier *= sprintSpeedMod; // Forge-Change

            _movementSpeedModifier.RefreshMovementSpeedModifiers(uid);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Updates the movement speed modifiers of an entity by applying or removing the <see cref="SlowedDownComponent"/>.
    /// If both walk and run modifiers are approximately 1 (i.e. normal speed) and <see cref="StaminaComponent.StaminaDamage"/> is 0,
    /// or if the both modifiers are 0, the slowdown component is removed to restore normal movement.
    /// Otherwise, the slowdown component is created or updated with the provided modifiers,
    /// and the movement speed is refreshed accordingly.
    /// </summary>
    /// <param name="ent">Entity whose movement speed should be updated.</param>
    /// <param name="walkSpeedModifier">New walk speed modifier. Default is 1f (normal speed).</param>
    /// <param name="runSpeedModifier">New run (sprint) speed modifier. Default is 1f (normal speed).</param>
    public void UpdateStunModifiers(Entity<StaminaComponent?> ent,
        float walkSpeedModifier = 1f,
        float runSpeedModifier = 1f)
    {
        if (!Resolve(ent, ref ent.Comp))
            return;

        if (
            (MathHelper.CloseTo(walkSpeedModifier, 1f) && MathHelper.CloseTo(runSpeedModifier, 1f) && ent.Comp.StaminaDamage == 0f) ||
            (walkSpeedModifier == 0f && runSpeedModifier == 0f)
        )
        {
            RemComp<SlowedDownComponent>(ent);
            return;
        }

        EnsureComp<SlowedDownComponent>(ent, out var comp);

        comp.WalkSpeedModifier = walkSpeedModifier;

        comp.SprintSpeedModifier = runSpeedModifier;

        _movementSpeedModifier.RefreshMovementSpeedModifiers(ent);

        Dirty(ent);
    }

    /// <summary>
    /// A convenience overload of <see cref="UpdateStunModifiers(EntityUid, float, float, StaminaComponent?)"/> that sets both
    /// walk and run speed modifiers to the same value.
    /// </summary>
    /// <param name="ent">Entity whose movement speed should be updated.</param>
    /// <param name="speedModifier">New walk and run speed modifier. Default is 1f (normal speed).</param>
    /// <param name="component">
    /// Optional <see cref="StaminaComponent"/> of the entity.
    /// </param>
    public void UpdateStunModifiers(Entity<StaminaComponent?> ent, float speedModifier = 1f)
    {
        UpdateStunModifiers(ent, speedModifier, speedModifier);
    }

    #region friction and movement listeners

    private void OnRefreshMovespeed(EntityUid ent, SlowedDownComponent comp, RefreshMovementSpeedModifiersEvent args) // Forge-Change
    {
        args.ModifySpeed(comp.WalkSpeedModifier, comp.SprintSpeedModifier); // Forge-Change
    }

    #endregion

    #region Attempt Event Handling

    private void OnMoveAttempt(EntityUid uid, StunnedComponent stunned, UpdateCanMoveEvent args)
    {
        if (stunned.LifeStage > ComponentLifeStage.Running)
            return;

        args.Cancel();
    }

    private void OnAttempt(EntityUid uid, StunnedComponent stunned, CancellableEntityEventArgs args)
    {
        args.Cancel();
    }

    private void OnEquipAttempt(EntityUid uid, StunnedComponent stunned, IsEquippingAttemptEvent args)
    {
        // is this a self-equip, or are they being stripped?
        if (args.Equipee == uid)
            args.Cancel();
    }

    private void OnUnequipAttempt(EntityUid uid, StunnedComponent stunned, IsUnequippingAttemptEvent args)
    {
        // is this a self-equip, or are they being stripped?
        if (args.Unequipee == uid)
            args.Cancel();
    }

    #endregion
}
