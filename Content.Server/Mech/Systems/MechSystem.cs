using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Components; // Forge-Change
using Content.Server.Body.Systems; // Forge-Change
using Content.Server.Hands.Systems; // Forge-Change
using Content.Server.Emp; // Forge-Change
using Content.Server.Mech.Components;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell; // Forge-Change
using Content.Shared._Forge.Emp; // Forge-Change
using Content.Shared.PowerCell; // Forge-Change
using Content.Shared.ActionBlocker;
using Content.Shared.Actions; // Frontier
using Content.Shared.Atmos; // Forge-Change
using Content.Shared.Atmos.Components; // Forge-Change
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Examine; // Forge-Change
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components; // Forge-Change
using Content.Shared.Interaction;
using Content.Shared.Toggleable; // Frontier
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.EntitySystems;
using Content.Shared.Mech.Equipment.Components; // Forge-Change
using Content.Shared.Movement.Components; // Forge-Change
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Content.Shared.Tools.Components;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Content.Shared.Tools.Systems;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems; // Frontier
using Robust.Shared.GameObjects; // Frontier
using Robust.Shared.Containers;
using Robust.Shared.Timing; // Forge-Change
using Robust.Shared.Map; // Forge-Change
using Robust.Shared.Map.Components; // Forge-Change
using Robust.Shared.Physics.Components; // Forge-Change
using Robust.Shared.Player;
using Content.Shared.Whitelist;
using Content.Shared.Mobs.Components; // Frontier
using Content.Shared.NPC.Components; // Frontier
using Content.Shared.Mobs; // Frontier
using Content.Shared.NPC.Systems; // Frontier

namespace Content.Server.Mech.Systems;

/// <inheritdoc/>
public sealed partial class MechSystem : SharedMechSystem
{
    [Dependency] private readonly IEntityManager _entityManager = default!; // Forge-Change
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly AtmosphereSystem _atmosphere = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!; // Forge-Change
    [Dependency] private readonly SharedActionsSystem _actions = default!; // Forge-Change
    [Dependency] private readonly BatterySystem _battery = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!; // Forge-Change
    [Dependency] private readonly SharedMapSystem _mapSystem = default!; // Forge-Change
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly SharedToolSystem _toolSystem = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!; // Frontier
    [Dependency] private readonly SharedPointLightSystem _light = default!; // Forge-Change
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] protected readonly IGameTiming Timing = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MechComponent, ToggleActionEvent>(OnToggleLightEvent); // Forge-Change
        SubscribeLocalEvent<MechComponent, MechToggleThrustersEvent>(OnMechToggleThrusters); // Forge-Change
        SubscribeLocalEvent<MechComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<MechComponent, EntInsertedIntoContainerMessage>(OnInsertEquipment); // Forge-Change
        SubscribeLocalEvent<MechComponent, EntRemovedFromContainerMessage>(OnItemRemoved); // Forge-Change
        SubscribeLocalEvent<MechComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<MechComponent, GetVerbsEvent<AlternativeVerb>>(OnAlternativeVerb);
        SubscribeLocalEvent<MechComponent, MechOpenUiEvent>(OnOpenUi);
        SubscribeLocalEvent<MechComponent, RemoveBatteryEvent>(OnRemoveBattery);
        SubscribeLocalEvent<MechComponent, RemoveGasTankEvent>(OnRemoveGasTank); // Forge-Change
        SubscribeLocalEvent<MechComponent, ChargeChangedEvent>(OnChargeChanged); // Forge-Change
        SubscribeLocalEvent<MechBatteryComponent, ChargeChangedEvent>(OnBatteryChargeChanged); // Forge-Change
        SubscribeLocalEvent<MechComponent, MechEntryEvent>(OnMechEntry);
        SubscribeLocalEvent<MechComponent, MechExitEvent>(OnMechExit);

        SubscribeLocalEvent<MechComponent, DamageChangedEvent>(OnDamageChanged);
        SubscribeLocalEvent<MechComponent, MechEquipmentRemoveMessage>(OnRemoveEquipmentMessage);

        SubscribeLocalEvent<MechPilotComponent, ToolUserAttemptUseEvent>(OnToolUseAttempt);
        SubscribeLocalEvent<MechPilotComponent, InhaleLocationEvent>(OnInhale);
        SubscribeLocalEvent<MechPilotComponent, ExhaleLocationEvent>(OnExhale);
        SubscribeLocalEvent<MechPilotComponent, AtmosExposedGetAirEvent>(OnExpose);
        SubscribeLocalEvent<MechAirComponent, AtmosDeviceUpdateEvent>(OnAirUpdate); // Forge-Change

        SubscribeLocalEvent<MechAirComponent, GetFilterAirEvent>(OnGetFilterAir);

        #region Equipment UI message relays
        // SubscribeLocalEvent<MechComponent, MechGrabberEjectMessage>(ReceiveEquipmentUiMesssages);    // Forge-Change - Moved to Shared
        // SubscribeLocalEvent<MechComponent, MechSoundboardPlayMessage>(ReceiveEquipmentUiMesssages);  // Forge-Change - Moved to Shared
        #endregion
    }


    // Forge-Change-Start
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var thrustDraw = EntityQueryEnumerator<MechThrustersComponent, MechComponent>();

        while (thrustDraw.MoveNext(out var uid, out var comp, out var mechComp))
        {
            if (!comp.ThrustersEnabled)
                continue;

            if (Timing.CurTime < comp.NextUpdateTime)
                continue;

            comp.NextUpdateTime = Timing.CurTime + comp.Delay;

            if (mechComp.BatterySlot.ContainedEntity is not { } batteryEnt
                || !TryComp<BatteryComponent>(batteryEnt, out var battery))
                continue;

            if (_battery.TryUseCharge(batteryEnt, comp.DrawRate))
            {
                var ev = new ChargeChangedEvent(battery.CurrentCharge, battery.MaxCharge);
                RaiseLocalEvent(uid, ref ev);
            }

            CreateParticles(uid);
        }
    }

    private void AttachBatteryComponents(EntityUid mech, EntityUid battery)
    {
        var mechBattery = EnsureComp<MechBatteryComponent>(battery);
        mechBattery.Mech = mech;

        EnsureComp<EmpProtectionComponent>(battery);
    }

    private void OnItemRemoved(EntityUid mech, MechComponent mechComp, EntRemovedFromContainerMessage args)
    {
        Dirty(mech, mechComp);
        UpdateUserInterface(mech, mechComp);
    }

    private void CreateParticles(EntityUid uid)
    {
        var uidXform = Transform(uid);

        var coordinates = uidXform.Coordinates;
        var gridUid = _transform.GetGrid(coordinates);

        if (TryComp<MapGridComponent>(gridUid, out var grid))
        {
            coordinates = new EntityCoordinates(gridUid.Value, _mapSystem.WorldToLocal(gridUid.Value, grid, _transform.ToMapCoordinates(coordinates).Position));
        }
        else if (uidXform.MapUid != null)
        {
            coordinates = new EntityCoordinates(uidXform.MapUid.Value, _transform.GetWorldPosition(uidXform));
        }
        else
        {
            return;
        }

        Spawn("JetpackEffect", coordinates);
    }

    private void OnMechToggleThrusters(EntityUid uid, MechComponent component, MechToggleThrustersEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MechThrustersComponent>(uid, out var mechThrusters))
            return;

        args.Handled = true;

        mechThrusters.ThrustersEnabled = !mechThrusters.ThrustersEnabled;

        _actions.SetToggled(component.MechToggleThrustersActionEntity, mechThrusters.ThrustersEnabled);

        if (mechThrusters.ThrustersEnabled)
        {
            AddComp<CanMoveInAirComponent>(uid);
            AddComp<MovementAlwaysTouchingComponent>(uid);
        }
        else
        {
            RemComp<CanMoveInAirComponent>(uid);
            RemComp<MovementAlwaysTouchingComponent>(uid);
        }

        Dirty(uid, mechThrusters);
    }

    private void OnToggleLightEvent(EntityUid uid, MechComponent component, ToggleActionEvent args)
    {
        if (args.Handled)
            return;

        if (component.BatterySlot.ContainedEntity == null
            || !TryComp<BatteryComponent>(component.BatterySlot.ContainedEntity, out var battery)
            || battery.CurrentCharge <= 0)
            return;

        args.Handled = true;

        ToggleLight(uid, component);
    }

    private void OnChargeChanged(EntityUid uid, MechComponent component, ref ChargeChangedEvent args)
    {
        if (args.Charge == 0 && component.Light)
            ToggleLight(uid, component);

        component.Energy = args.Charge;
        component.MaxEnergy = args.MaxCharge;

        UpdateCanMove(uid, component);
        UpdateUserInterface(uid, component);

        Dirty(uid, component);
    }

    private void OnBatteryChargeChanged(EntityUid uid, MechBatteryComponent component, ref ChargeChangedEvent args)
    {
        if (!TryComp<MechComponent>(component.Mech, out var mechComp))
            return;

        var mech = component.Mech;

        if (args.Charge == 0 && mechComp.Light)
            ToggleLight(mech, mechComp);

        mechComp.Energy = args.Charge;
        mechComp.MaxEnergy = args.MaxCharge;

        UpdateCanMove(mech, mechComp);
        UpdateUserInterface(mech, mechComp);

        Dirty(mech, mechComp);
    }

    public void ToggleLight(EntityUid uid, MechComponent component)
    {
        if (!_light.TryGetLight(uid, out var light))
            return;

        _light.SetEnabled(uid, !component.Light, comp: light);

        _actions.SetToggled(component.MechToggleLightActionEntity, !component.Light);

        _audioSystem.PlayPredicted(component.ToggleLightSound, uid, uid);

        component.Light = !component.Light;

        Dirty(uid, component);

        UpdateAppearance(uid, component);
    }

    private void UpdateCanMove(EntityUid mech, MechComponent? mechComp = null)
    {
        if (!Resolve(mech, ref mechComp))
            return;

        _actionBlocker.UpdateCanMove(mech);
        if (mechComp.PilotSlot.ContainedEntity is { } pilot)
            _actionBlocker.UpdateCanMove(pilot);
    }
    // Forge-Change-End

    private void OnInteractUsing(EntityUid uid, MechComponent component, InteractUsingEvent args)
    {
        if (TryComp<WiresPanelComponent>(uid, out var panel) && !panel.Open)
            return;

        if (component.BatterySlot.ContainedEntity == null
            && TryComp<BatteryComponent>(args.Used, out var battery)
            && _whitelistSystem.IsWhitelistPass(component.GasTankWhitelist, args.Used))
        {
            InsertBattery(uid, args.Used, component, battery);
            UpdateCanMove(uid, component);
            return;
        }

        if (component.GasTankSlot.ContainedEntity == null
            && TryComp<GasTankComponent>(args.Used, out var gasTank)
            && _whitelistSystem.IsWhitelistPass(component.BatteryWhitelist, args.Used))
        {
            InsertGasTank(uid, args.Used, component, gasTank);
        }

        if (_toolSystem.HasQuality(args.Used, "Prying"))
        {
            if (component.BatterySlot.ContainedEntity != null)
            {
                var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.BatteryRemovalDelay,
                    new RemoveBatteryEvent(), uid, target: uid, used: args.Target)
                {
                    BreakOnMove = true
                };

                _doAfter.TryStartDoAfter(doAfterEventArgs);
            }
            else if (component.GasTankSlot.ContainedEntity != null)
            {
                var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.BatteryRemovalDelay,
                    new RemoveGasTankEvent(), uid, target: uid, used: args.Target)
                {
                    BreakOnMove = true
                };

                _doAfter.TryStartDoAfter(doAfterEventArgs);
            }
        }
    }

    private void OnInsertEquipment(EntityUid uid, MechComponent component, EntInsertedIntoContainerMessage args)
    {
        UpdateUserInterface(uid, component);
        if (!(args.Container != component.BatterySlot || !TryComp<BatteryComponent>(args.Entity, out var battery)))
        {
            AttachBatteryComponents(uid, args.Entity);

            component.Energy = battery.CurrentCharge;
            component.MaxEnergy = battery.MaxCharge;

            Dirty(uid, component);
            UpdateCanMove(uid, component);
        }
        else if(!(args.Container != component.GasTankSlot || !TryComp<GasTankComponent>(args.Entity, out var gasTank)))
        {
            Dirty(uid, component);
            UpdateCanMove(uid, component);
        }
        else
        {
            return;
        }
    }

    private void OnRemoveBattery(EntityUid uid, MechComponent component, RemoveBatteryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        RemoveBattery(uid, component);
        UpdateCanMove(uid, component);

        args.Handled = true;
    }

    private void OnRemoveGasTank(EntityUid uid, MechComponent component, RemoveGasTankEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        _container.EmptyContainer(component.GasTankSlot);

        args.Handled = true;
    }

    private void OnMapInit(EntityUid uid, MechComponent component, MapInitEvent args)
    {
        var xform = Transform(uid);
        // TODO: this should use containerfill?
        foreach (var equipment in component.StartingEquipment)
        {
            var ent = Spawn(equipment, xform.Coordinates);
            InsertEquipment(uid, ent, component);
        }

        // TODO: this should just be damage and battery
        component.Integrity = component.MaxIntegrity;
        component.Energy = component.MaxEnergy;

        // Forge-Change-start
        if (component.BatterySlot.ContainedEntity is { } battery)
        {
            AttachBatteryComponents(uid, battery);
        }
        // Forge-Change-end

        UpdateCanMove(uid, component);
        Dirty(uid, component);
    }

    private void OnRemoveEquipmentMessage(EntityUid uid, MechComponent component, MechEquipmentRemoveMessage args)
    {
        // Frontier: mechs with fixed equipment
        if (!component.CanRemoveEquipment)
            return;
        // End Frontier: mechs with fixed equipment

        var equip = GetEntity(args.Equipment);

        if (!Exists(equip) || Deleted(equip))
            return;

        if (!component.EquipmentContainer.ContainedEntities.Contains(equip))
            return;

        RemoveEquipment(uid, equip, component);
    }

    private void OnOpenUi(EntityUid uid, MechComponent component, MechOpenUiEvent args)
    {
        args.Handled = true;
        ToggleMechUi(uid, component);
    }

    private void OnToolUseAttempt(EntityUid uid, MechPilotComponent component, ref ToolUserAttemptUseEvent args)
    {
        if (args.Target == component.Mech)
            args.Cancelled = true;
    }

    private void OnAlternativeVerb(EntityUid uid, MechComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || component.Broken)
            return;

        if (CanInsert(uid, args.User, component))
        {
            var enterVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mech-verb-enter"),
                Act = () =>
                {
                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.EntryDelay, new MechEntryEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };

                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            var openUiVerb = new AlternativeVerb //can't hijack someone else's mech
            {
                Act = () => ToggleMechUi(uid, component, args.User),
                Text = Loc.GetString("mech-ui-open-verb")
            };
            args.Verbs.Add(enterVerb);
            args.Verbs.Add(openUiVerb);
        }
        else if (!IsEmpty(component))
        {
            var ejectVerb = new AlternativeVerb
            {
                Text = Loc.GetString("mech-verb-exit"),
                Priority = 1, // Promote to top to make ejecting the ALT-click action
                Act = () =>
                {
                    if (args.User == uid || args.User == component.PilotSlot.ContainedEntity)
                    {
                        TryEject(uid, component);
                        return;
                    }

                    var doAfterEventArgs = new DoAfterArgs(EntityManager, args.User, component.ExitDelay, new MechExitEvent(), uid, target: uid)
                    {
                        BreakOnMove = true,
                    };
                    _popup.PopupEntity(Loc.GetString("mech-eject-pilot-alert", ("item", uid), ("user", args.User)), uid, PopupType.Large);

                    _doAfter.TryStartDoAfter(doAfterEventArgs);
                }
            };
            args.Verbs.Add(ejectVerb);
        }
    }

    private void OnMechEntry(EntityUid uid, MechComponent component, MechEntryEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        if (_whitelistSystem.IsWhitelistFail(component.PilotWhitelist, args.User))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-enter", ("item", uid)), args.User);
            return;
        }

        // Frontier - Make AI Attack mechs based on user.
        if (TryComp<MobStateComponent>(args.User, out var _))
        {
            component.MobStateAdded = !EnsureComp<MobStateComponent>(uid, out _);
            component.MobThresholdsAdded = !EnsureComp<MobThresholdsComponent>(uid, out _);
        }
        if (TryComp<NpcFactionMemberComponent>(args.User, out var faction))
        {
            component.NpcFactionAdded = !EnsureComp<NpcFactionMemberComponent>(uid, out var factionMech);
            _npcFaction.AddFactions((uid, factionMech), faction.Factions);
        }
        // End Frontier

        TryInsert(uid, args.Args.User, component);
        UpdateCanMove(uid, component);

        args.Handled = true;
    }

    private void OnMechExit(EntityUid uid, MechComponent component, MechExitEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        TryEject(uid, component);

        // Frontier: revert state
        if (component.MobStateAdded)
        {
            RemComp<MobStateComponent>(uid);
            component.MobStateAdded = false;
        }
        if (component.MobThresholdsAdded)
        {
            RemComp<MobThresholdsComponent>(uid);
            component.MobThresholdsAdded = false;
        }
        if (component.NpcFactionAdded)
        {
            RemComp<NpcFactionMemberComponent>(uid);
            component.NpcFactionAdded = false;
        }
        // End Frontier: revert state

        args.Handled = true;
    }

    private void OnDamageChanged(EntityUid uid, MechComponent component, DamageChangedEvent args)
    {
        var integrity = component.MaxIntegrity - args.Damageable.TotalDamage;
        SetIntegrity(uid, integrity, component);

        if (args.DamageIncreased &&
            args.DamageDelta != null &&
            component.PilotSlot.ContainedEntity != null)
        {
            var damage = args.DamageDelta * component.MechToPilotDamageMultiplier;
            _damageable.TryChangeDamage(component.PilotSlot.ContainedEntity, damage);
        }

        if (TryComp<MobStateComponent>(component.PilotSlot.ContainedEntity, out var state) && state.CurrentState != MobState.Alive) // Frontier - Eject players from mechs when they go crit
            TryEject(uid, component);
    }

    private void ToggleMechUi(EntityUid uid, MechComponent? component = null, EntityUid? user = null)
    {
        if (!Resolve(uid, ref component))
            return;
        user ??= component.PilotSlot.ContainedEntity;
        if (user == null)
            return;

        if (!TryComp<ActorComponent>(user, out var actor))
            return;

        _ui.TryToggleUi(uid, MechUiKey.Key, actor.PlayerSession);
        UpdateUserInterface(uid, component);
    }

    public override void UpdateUserInterface(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        base.UpdateUserInterface(uid, component);

        var ev = new MechEquipmentUiStateReadyEvent();
        foreach (var ent in component.EquipmentContainer.ContainedEntities)
        {
            RaiseLocalEvent(ent, ev);
        }

        var state = new MechBoundUiState
        {
            EquipmentStates = ev.States,
            Equipment = component.EquipmentContainer.ContainedEntities.Select(o => _entityManager.GetNetEntity(o)).ToList() // Forge-Change
        };
        _ui.SetUiState(uid, MechUiKey.Key, state);
    }

    public override void BreakMech(EntityUid uid, MechComponent? component = null)
    {
        base.BreakMech(uid, component);

        _ui.CloseUi(uid, MechUiKey.Key);
        UpdateCanMove(uid, component);
    }

    public override bool TryChangeEnergy(EntityUid uid, FixedPoint2 delta, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!base.TryChangeEnergy(uid, delta, component))
            return false;

        var battery = component.BatterySlot.ContainedEntity;
        if (battery == null)
            return false;

        if (!TryComp<BatteryComponent>(battery, out var batteryComp))
            return false;

        _battery.SetCharge(battery!.Value, batteryComp.CurrentCharge + delta.Float(), batteryComp);
        if (batteryComp.CurrentCharge != component.Energy) //if there's a discrepency, we have to resync them
        {
            Log.Debug($"Battery charge was not equal to mech charge. Battery {batteryComp.CurrentCharge}. Mech {component.Energy}");
            component.Energy = batteryComp.CurrentCharge;
            Dirty(uid, component);
        }
        UpdateCanMove(uid, component);
        return true;
    }

    public void InsertGasTank(EntityUid uid, EntityUid toInsert, MechComponent? component = null, GasTankComponent? gasTank = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (!Resolve(toInsert, ref gasTank, false))
            return;

        _container.Insert(toInsert, component.GasTankSlot);
        UpdateCanMove(uid, component);
        Dirty(uid, component);
    }

    public void InsertBattery(EntityUid uid, EntityUid toInsert, MechComponent? component = null, BatteryComponent? battery = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        TryComp(toInsert, out battery);

        AttachBatteryComponents(uid, toInsert); // Forge-Change

        _container.Insert(toInsert, component.BatterySlot);

        if (battery != null)
        {
            component.Energy = battery.CurrentCharge;
            component.MaxEnergy = battery.MaxCharge;
        }

        UpdateCanMove(uid, component);
        Dirty(uid, component);
        UpdateUserInterface(uid, component);
    }

    public void RemoveBattery(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component) || component.BatterySlot.ContainedEntity == null ) // Forge-Change
            return;

        RemComp<MechBatteryComponent>(component.BatterySlot.ContainedEntity.Value); // Forge-Change
        RemComp<EmpProtectionComponent>(component.BatterySlot.ContainedEntity.Value); // Forge-Change

        _container.EmptyContainer(component.BatterySlot);
        component.Energy = 0;
        component.MaxEnergy = 0;

        UpdateCanMove(uid, component);

        Dirty(uid, component);
        UpdateUserInterface(uid, component);
    }

    #region Atmos Handling
    private void OnInhale(EntityUid uid, MechPilotComponent component, InhaleLocationEvent args)
    {
        if (!TryComp<MechComponent>(component.Mech, out var mech) ||
            !TryComp<MechAirComponent>(component.Mech, out var mechAir))
        {
            return;
        }

        if (mech.Airtight)
            args.Gas = mechAir.Air;
    }

    private void OnExhale(EntityUid uid, MechPilotComponent component, ExhaleLocationEvent args)
    {
        if (!TryComp<MechComponent>(component.Mech, out var mech) ||
            !TryComp<MechAirComponent>(component.Mech, out var mechAir))
        {
            return;
        }

        if (mech.Airtight)
            args.Gas = mechAir.Air;
    }

    private void OnExpose(EntityUid uid, MechPilotComponent component, ref AtmosExposedGetAirEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp(component.Mech, out MechComponent? mech))
            return;

        if (mech.Airtight && TryComp(component.Mech, out MechAirComponent? air))
        {
            args.Handled = true;
            args.Gas = air.Air;
            return;
        }

        args.Gas =  _atmosphere.GetContainingMixture(component.Mech, excite: args.Excite);
        args.Handled = true;
    }

    private void OnAirUpdate(EntityUid uid, MechAirComponent comp, ref AtmosDeviceUpdateEvent args)
    {
        if (!TryComp<MechComponent>(uid, out var mech) || !mech.Airtight || mech.GasTankSlot.ContainedEntity == null || !mech.Internals)
            return;

        var gasTank = Comp<GasTankComponent>(mech.GasTankSlot.ContainedEntity.Value);
        _atmosphere.PumpGasTo(gasTank.Air, comp.Air, 70);
    }

    private void OnGetFilterAir(EntityUid uid, MechAirComponent comp, ref GetFilterAirEvent args)
    {
        if (args.Air != null)
            return;

        // only airtight mechs get internal air
        if (!TryComp<MechComponent>(uid, out var mech) || !mech.Airtight)
            return;

        args.Air = comp.Air;
    }
    #endregion
}
