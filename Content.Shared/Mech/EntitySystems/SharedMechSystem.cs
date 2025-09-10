using System.Linq;
using Content.Shared._Forge.ForgeVars;
using Content.Shared._Forge.Mech; // Forge-Change
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems; // Forge-Change
using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Emag.Systems; // Forge-Change
using Content.Shared.FixedPoint;
using Content.Shared.Hands.Components; // Forge-Change
using Content.Shared.Hands.EntitySystems; // Forge-Change
using Content.Shared.Implants.Components; // Forge-Change
using Content.Shared.Inventory.VirtualItem; // Forge-Change
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Mech.Components;
using Content.Shared.Mech.Equipment.Components;
using Content.Shared.Mech.Events; // Forge-Change
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Movement.Events; // Forge-Change
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Events; // Forge-Change
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems; // Forge-Change
using Robust.Shared.GameObjects; // Forge-Change
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;
using Content.Shared.Mobs.Components; // Frontier
using Content.Shared.NPC.Components;
using Content.Shared._NF.Mech.Equipment.Events; // Frontier

namespace Content.Shared.Mech.EntitySystems;

/// <summary>
/// Handles all of the interactions, UI handling, and items shennanigans for <see cref="MechComponent"/>
/// </summary>
public abstract partial class SharedMechSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _accessReader = default!; // Forge-Change
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelistSystem = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!; // Forge-Change
    [Dependency] private readonly SharedVirtualItemSystem _virtualItem = default!; // Forge-Change
    [Dependency] private readonly IConfigurationManager _config = default!; // Forge-Change
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!; // Forge-Change
    [Dependency] private readonly SharedPointLightSystem _light = default!; // Forge-Change

    // Forge-Change: Local variable for checking if mech guns can be used out of them.
    private bool _canUseMechGunOutside;

    /// <inheritdoc/>
    public override void Initialize()
    {
        // SubscribeLocalEvent<MechComponent, MechToggleEquipmentEvent>(OnToggleEquipmentAction);
        SubscribeLocalEvent<MechComponent, MechToggleInternalsEvent>(OnMechToggleInternals);
        SubscribeLocalEvent<MechComponent, MechEjectPilotEvent>(OnEjectPilotEvent);
        SubscribeLocalEvent<MechComponent, UserActivateInWorldEvent>(RelayInteractionEvent);
        SubscribeLocalEvent<MechComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<MechComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<MechComponent, GetAdditionalAccessEvent>(OnGetAdditionalAccess);
        SubscribeLocalEvent<MechComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<MechComponent, CanDropTargetEvent>(OnCanDragDrop);
        SubscribeLocalEvent<MechComponent, GotEmaggedEvent>(OnEmagged); // Forge-Change

        SubscribeLocalEvent<MechPilotComponent, GetMeleeWeaponEvent>(OnGetMeleeWeapon);
        SubscribeLocalEvent<MechPilotComponent, CanAttackFromContainerEvent>(OnCanAttackFromContainer);
        SubscribeLocalEvent<MechPilotComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<MechPilotComponent, EntGotRemovedFromContainerMessage>(OnPilotRemoved); // Forge-Change

        SubscribeLocalEvent<MechComponent, UpdateCanMoveEvent>(OnMechMoveEvent); // Forge-Change
        SubscribeLocalEvent<MechPilotComponent, UpdateCanMoveEvent>(OnPilotMoveEvent); // Forge-Change
        SubscribeLocalEvent<MechComponent, ChangeDirectionAttemptEvent>(OnMechMoveEvent); // Forge-Change

        SubscribeLocalEvent<MechEquipmentComponent, ShotAttemptedEvent>(OnShotAttempted); // Forge-Change
        Subs.CVar(_config, ForgeVars.MechGunOutsideMech, value => _canUseMechGunOutside = value, true); // Forge-Change

        // Forge-Change
        SubscribeNetworkEvent<SelectMechEquipmentEvent>(OnMechEquipSelected);

        SubscribeLocalEvent<MechComponent, MechGrabberEjectMessage>(ReceiveEquipmentUiMesssages);
        SubscribeLocalEvent<MechComponent, MechSoundboardPlayMessage>(ReceiveEquipmentUiMesssages);

        InitializeForge();
    }

    private void OnPilotMoveEvent(EntityUid uid, MechPilotComponent component, UpdateCanMoveEvent args) // Forge-Change
    {
        if (component.LifeStage > ComponentLifeStage.Running || !TryComp<MechComponent>(component.Mech, out var mech))
            return;

        if (mech.Broken || mech.Integrity <= 0 || mech.Energy <= 0)
            args.Cancel();
    }
    
    private void OnMechMoveEvent(EntityUid uid, MechComponent component, CancellableEntityEventArgs args) // Forge-Change
    {
        if (component.LifeStage > ComponentLifeStage.Running)
            return;

        if (component.Broken || component.Integrity <= 0 || component.Energy <= 0)
            args.Cancel();
    }

    // Forge-Change: Fixes scram implants or teleports locking the pilot out of being able to move.
    private void OnEntGotRemovedFromContainer(EntityUid uid, MechPilotComponent component, EntGotRemovedFromContainerMessage args)
    {
        TryEject(component.Mech, pilot: uid);
    }

    private void OnMechToggleInternals(EntityUid uid, MechComponent component, MechToggleInternalsEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        
        component.Internals = !component.Internals;
        
        _actions.SetToggled(component.MechToggleInternalsActionEntity, component.Internals);
    }

    private void OnEjectPilotEvent(EntityUid uid, MechComponent component, MechEjectPilotEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        TryEject(uid, component);
    }

    private void RelayInteractionEvent(EntityUid uid, MechComponent component, UserActivateInWorldEvent args)
    {
        var pilot = component.PilotSlot.ContainedEntity;
        if (pilot == null)
            return;

        // TODO why is this being blocked?
        if (!_timing.IsFirstTimePredicted)
            return;

        if (component.CurrentSelectedEquipment != null)
        {
            RaiseLocalEvent(component.CurrentSelectedEquipment.Value, args);
        }
    }

    private void OnStartup(EntityUid uid, MechComponent component, ComponentStartup args)
    {
        component.PilotSlot = _container.EnsureContainer<ContainerSlot>(uid, component.PilotSlotId);
        component.EquipmentContainer = _container.EnsureContainer<Container>(uid, component.EquipmentContainerId);
        component.BatterySlot = _container.EnsureContainer<ContainerSlot>(uid, component.BatterySlotId);
        component.GasTankSlot = _container.EnsureContainer<ContainerSlot>(uid, component.GasTankSlotId);
        component.CapacitorSlot = _container.EnsureContainer<ContainerSlot>(uid, component.CapacitorSlotId);
        UpdateAppearance(uid, component);
    }

    private void OnDestruction(EntityUid uid, MechComponent component, DestructionEventArgs args)
    {
        BreakMech(uid, component);
    }

    private void OnGetAdditionalAccess(EntityUid uid, MechComponent component, ref GetAdditionalAccessEvent args)
    {
        var pilot = component.PilotSlot.ContainedEntity;
        if (pilot == null)
            return;

        args.Entities.Add(pilot.Value);
    }

    private void SetupUser(EntityUid mech, EntityUid pilot, MechComponent? component = null)
    {
        if (!Resolve(mech, ref component))
            return;

        var rider = EnsureComp<MechPilotComponent>(pilot);

        // Warning: this bypasses most normal interaction blocking components on the user, like drone laws and the like.
        var irelay = EnsureComp<InteractionRelayComponent>(pilot);

        _mover.SetRelay(pilot, mech);
        _interaction.SetRelay(pilot, mech, irelay);
        rider.Mech = mech;
        Dirty(pilot, rider);

        if ((component.Integrity / component.MaxIntegrity) * 100 >= 50 )
            if (component.FirstStart)
            {
                _audioSystem.PlayEntity(component.NominalLongSound, pilot, mech);
                component.FirstStart = false;
                Dirty(mech, component);
            }
            else
                _audioSystem.PlayEntity(component.NominalSound, pilot, mech);
        else
            _audioSystem.PlayEntity(component.CriticalDamageSound, pilot, mech);

        UpdateActions(mech, pilot, component);
    }
    
    private void UpdateActions(EntityUid mech, EntityUid pilot, MechComponent? component = null)
    {
        if (!Resolve(mech, ref component))
            return;

        if (_net.IsClient)
            return;

        _actions.AddAction(pilot, ref component.MechCycleActionEntity, component.MechCycleAction, mech);
        _actions.AddAction(pilot, ref component.MechUiActionEntity, component.MechUiAction, mech);
        _actions.AddAction(pilot, ref component.MechEjectActionEntity, component.MechEjectAction, mech);
        if (_light.TryGetLight(mech, out var light))
            _actions.AddAction(pilot, ref component.MechToggleLightActionEntity, component.MechToggleLightAction, mech); // Forge-Change
        if (component.Airtight)
            _actions.AddAction(pilot, ref component.MechToggleInternalsActionEntity, component.MechToggleInternalsAction, mech); // Forge-Change
        if (HasComp<MechThrustersComponent>(mech))
            _actions.AddAction(pilot, ref component.MechToggleThrustersActionEntity, component.MechToggleThrustersAction, mech); // Forge-Change
        var equipment = new List<EntityUid>(component.EquipmentContainer.ContainedEntities);
        foreach (var ent in equipment)
            if (TryComp<MechEquipmentActionComponent>(ent, out var actionComp))
                _actions.AddAction(pilot, ref actionComp.EquipmentActionEntity, actionComp.EquipmentAction, ent);

        RaiseEquipmentEquippedEvent((mech, component), pilot); // Frontier (note: must send pilot separately, not yet in their seat)
    }

    private void RemoveUser(EntityUid mech, EntityUid pilot)
    {
        if (!RemComp<MechPilotComponent>(pilot))
            return;
        RemComp<RelayInputMoverComponent>(pilot);
        RemComp<InteractionRelayComponent>(pilot);

        _actions.RemoveProvidedActions(pilot, mech);

        if (!TryComp<MechComponent>(mech, out var mechComp))
            return;
        var equipment = new List<EntityUid>(mechComp.EquipmentContainer.ContainedEntities);
        foreach (var ent in equipment)
            if (TryComp<MechEquipmentActionComponent>(ent, out var actionComp))
                _actions.RemoveProvidedActions(pilot, ent);
    }

    /// <summary>
    /// Destroys the mech, removing the user and ejecting all installed equipment.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    public virtual void BreakMech(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        TryEject(uid, component);
        var equipment = new List<EntityUid>(component.EquipmentContainer.ContainedEntities);
        // Frontier: optionally removable equipment
        if (component.CanRemoveEquipment)
        {
            foreach (var ent in equipment)
            {
                RemoveEquipment(uid, ent, component, forced: true);
            }
        }
        // End Frontier

        component.Broken = true;
        UpdateAppearance(uid, component);
    }

    /// <summary>
    /// Cycles through the currently selected equipment.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="component"></param>
    public void CycleEquipment(EntityUid uid, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        var allEquipment = component.EquipmentContainer.ContainedEntities.ToList();

        var equipmentIndex = -1;
        if (component.CurrentSelectedEquipment != null)
        {
            bool StartIndex(EntityUid u) => u == component.CurrentSelectedEquipment;
            equipmentIndex = allEquipment.FindIndex(StartIndex);
        }

        // Frontier
        if (component.PilotSlot.ContainedEntity != null && component.CurrentSelectedEquipment != null)
            _actions.RemoveProvidedActions(component.PilotSlot.ContainedEntity.Value, component.CurrentSelectedEquipment.Value);
        // End Frontier

        equipmentIndex++;
        component.CurrentSelectedEquipment = equipmentIndex >= allEquipment.Count
            ? null
            : allEquipment[equipmentIndex];

        var popupString = component.CurrentSelectedEquipment != null
            ? Loc.GetString("mech-equipment-select-popup", ("item", component.CurrentSelectedEquipment))
            : Loc.GetString("mech-equipment-select-none-popup");

        if (_net.IsServer)
            _popup.PopupEntity(popupString, uid);

        RaiseEquipmentEquippedEvent((uid, component)); // Frontier

        Dirty(uid, component);
    }

    /// <summary>
    /// Inserts an equipment item into the mech.
    /// </summary>
    /// <param name="uid"> Mech </param>
    /// <param name="toInsert"> Equipment what inserted </param>
    /// <param name="component"> Mech Component </param>
    /// <param name="equipmentComponent"> Equipment Component </param>
    public void InsertEquipment(EntityUid uid, EntityUid toInsert, MechComponent? component = null,
        MechEquipmentComponent? equipmentComponent = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!Resolve(toInsert, ref equipmentComponent))
            return;

        if (component.EquipmentContainer.ContainedEntities.Count >= component.MaxEquipmentAmount)
            return;

        if (_whitelistSystem.IsWhitelistFail(component.EquipmentWhitelist, toInsert))
            return;

        if (!TryComp<MetaDataComponent>(toInsert, out var toInsertMeta))
            return;

        var equipment = new List<EntityUid>(component.EquipmentContainer.ContainedEntities);
        foreach (var ent in equipment)
            if (TryComp<MetaDataComponent>(ent, out var entMeta) && entMeta.EntityPrototype == toInsertMeta.EntityPrototype)
                return;

        equipmentComponent.EquipmentOwner = uid;
        _container.Insert(toInsert, component.EquipmentContainer);
        var ev = new MechEquipmentInsertedEvent(uid);
        RaiseLocalEvent(toInsert, ref ev);
        if (component.PilotSlot.ContainedEntity != null)
            UpdateActions(uid, component.PilotSlot.ContainedEntity.Value, component);
    }

    /// <summary>
    /// Removes an equipment item from a mech.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="toRemove"></param>
    /// <param name="component"></param>
    /// <param name="equipmentComponent"></param>
    /// <param name="forced">Whether or not the removal can be cancelled</param>
    public void RemoveEquipment(EntityUid uid, EntityUid toRemove, MechComponent? component = null,
        MechEquipmentComponent? equipmentComponent = null, bool forced = false)
    {
        if (!Resolve(uid, ref component))
            return;

        if (!Resolve(toRemove, ref equipmentComponent))
            return;

        if (!forced)
        {
            var attemptev = new AttemptRemoveMechEquipmentEvent();
            RaiseLocalEvent(toRemove, ref attemptev);
            if (attemptev.Cancelled)
                return;
        }

        var ev = new MechEquipmentRemovedEvent(uid);
        RaiseLocalEvent(toRemove, ref ev);

        if (component.CurrentSelectedEquipment == toRemove)
            CycleEquipment(uid, component);

        equipmentComponent.EquipmentOwner = null;
        _container.Remove(toRemove, component.EquipmentContainer);
    }

    /// <summary>
    /// Attempts to change the amount of energy in the mech.
    /// </summary>
    /// <param name="uid">The mech itself</param>
    /// <param name="delta">The change in energy</param>
    /// <param name="component"></param>
    /// <returns>If the energy was successfully changed.</returns>
    public virtual bool TryChangeEnergy(EntityUid uid, FixedPoint2 delta, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if ((component.Energy / component.MaxEnergy) * 100 <= 25 
            && component.PlayPowerSound 
            && component.PilotSlot.ContainedEntity != null)
        {
            _audioSystem.PlayEntity(component.LowPowerSound, component.PilotSlot.ContainedEntity.Value, uid);
            
            component.PlayPowerSound = false;
        }
        else if ((component.Energy / component.MaxEnergy) * 100 >= 25)
            component.PlayPowerSound = true;

        component.Energy = FixedPoint2.Clamp(component.Energy + delta, 0, component.MaxEnergy);
        Dirty(uid, component);
        UpdateUserInterface(uid, component);
        return true;
    }

    /// <summary>
    /// Sets the integrity of the mech.
    /// </summary>
    /// <param name="uid">The mech itself</param>
    /// <param name="value">The value the integrity will be set at</param>
    /// <param name="component"></param>
    public void SetIntegrity(EntityUid uid, FixedPoint2 value, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.Integrity = FixedPoint2.Clamp(value, 0, component.MaxIntegrity);

        if ((component.Integrity / component.MaxIntegrity) * 100 <= 50 
            && component.PlayIntegritySound 
            && component.PilotSlot.ContainedEntity != null)
        {
            _audioSystem.PlayEntity(component.CriticalDamageSound, component.PilotSlot.ContainedEntity.Value, uid);
            
            component.PlayIntegritySound = false;
        }
        else if ((component.Integrity / component.MaxIntegrity) * 100 >= 50)
            component.PlayIntegritySound = true;

        if (component.Integrity <= 0)
        {
            BreakMech(uid, component);
        }
        else if (component.Broken)
        {
            component.Broken = false;
            UpdateAppearance(uid, component);
        }

        Dirty(uid, component);
        UpdateUserInterface(uid, component);
    }

    /// <summary>
    /// Checks if the pilot is present
    /// </summary>
    /// <param name="component"></param>
    /// <returns>Whether or not the pilot is present</returns>
    public bool IsEmpty(MechComponent component)
    {
        return component.PilotSlot.ContainedEntity == null;
    }

    /// <summary>
    /// Checks if an entity can be inserted into the mech.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="toInsert"></param>
    /// <param name="component"></param>
    /// <returns></returns>
    public bool CanInsert(EntityUid uid, EntityUid toInsert, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (TryComp<AccessReaderComponent>(uid, out var access) && !_accessReader.IsAllowed(toInsert, uid, access))
        {
            _popup.PopupEntity(Loc.GetString("mech-no-access-popup"), uid);
            return false;
        }

        return IsEmpty(component) && _actionBlocker.CanMove(toInsert);
    }

    /// <summary>
    /// Updates the user interface
    /// </summary>
    /// <remarks>
    /// This is defined here so that UI updates can be accessed from shared.
    /// </remarks>
    public virtual void UpdateUserInterface(EntityUid uid, MechComponent? component = null)
    {
    }

    /// <summary>
    /// Attempts to insert a pilot into the mech.
    /// </summary>
    /// <param name="uid"></param>
    /// <param name="toInsert"></param>
    /// <param name="component"></param>
    /// <returns>Whether or not the entity was inserted</returns>
    public bool TryInsert(EntityUid uid, EntityUid? toInsert, MechComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (toInsert == null || component.PilotSlot.ContainedEntity == toInsert)
            return false;

        if (!CanInsert(uid, toInsert.Value, component))
            return false;

        SetupUser(uid, toInsert.Value);
        _container.Insert(toInsert.Value, component.PilotSlot);
        UpdateAppearance(uid, component);
        // <Forge-Change>
        UpdateHands(toInsert.Value, uid, true);

        var ev = new MechInsertedEvent(uid);
        RaiseLocalEvent(toInsert.Value, ev);
        // </Forge-Change>
        return true;
    }

    /// <summary>
    /// Attempts to eject the current pilot from the mech
    /// </summary>
    /// <param name="uid"> mech </param>
    /// <param name="component"> mech component </param>
    /// <param name="pilot">The pilot to eject</param>
    /// <returns>Whether or not the pilot was ejected.</returns>
    public bool TryEject(EntityUid uid, MechComponent? component = null, EntityUid? pilot = null) // Forge-Change edit
    {
        if (!Resolve(uid, ref component))
            return false;

        if (component.PilotSlot.ContainedEntity != null) // Forge-Change edit
            pilot = component.PilotSlot.ContainedEntity.Value;

        if (pilot == null) // Forge-Change edit
            return false;

        if (HasComp<NoRotateOnMoveComponent>(uid)) // Forge-Change
            RemComp<NoRotateOnMoveComponent>(uid);

        RemoveUser(uid, pilot.Value); // Forge-Change edit
        _container.RemoveEntity(uid, pilot.Value); // Forge-Change edit
        UpdateAppearance(uid, component);

        // <Forge-Change>
        UpdateHands(pilot.Value, uid, false);

        var ev = new MechEjectedEvent(uid);
        RaiseLocalEvent(pilot.Value, ev);
        // </Forge-Change>

        // Frontier - Make NPC AI attack Mechs
        if (TryComp<MobStateComponent>(uid, out var _))
            RemComp<MobStateComponent>(uid);
        if (TryComp<NpcFactionMemberComponent>(uid, out var _))
            RemComp<NpcFactionMemberComponent>(uid);
        // Frontier

        return true;
    }

    private void OnPilotRemoved(EntityUid uid, MechPilotComponent component, EntGotRemovedFromContainerMessage args)
    {
        RemoveUser(component.Mech, uid);

        if (TryComp<MechComponent>(component.Mech, out var mechComp))
            UpdateAppearance(component.Mech, mechComp);
    }

    // Forge-Change Change Start
    private void UpdateHands(EntityUid uid, EntityUid mech, bool active)
    {
        if (!TryComp<HandsComponent>(uid, out var handsComponent))
            return;

        if (active)
            BlockHands(uid, mech, handsComponent);
        else
            FreeHands(uid, mech);
    }

    private void BlockHands(EntityUid uid, EntityUid mech, HandsComponent handsComponent)
    {
        var freeHands = 0;
        foreach (var hand in _hands.EnumerateHands(uid, handsComponent))
        {
            if (hand.HeldEntity == null)
            {
                freeHands++;
                continue;
            }

            // Is this entity removable? (they might have handcuffs on)
            if (HasComp<UnremoveableComponent>(hand.HeldEntity) && hand.HeldEntity != mech)
                continue;

            _hands.DoDrop(uid, hand, true, handsComponent);
            freeHands++;
            if (freeHands == 2)
                break;
        }
        if (_virtualItem.TrySpawnVirtualItemInHand(mech, uid, out var virtItem1))
            EnsureComp<UnremoveableComponent>(virtItem1.Value);

        if (_virtualItem.TrySpawnVirtualItemInHand(mech, uid, out var virtItem2))
            EnsureComp<UnremoveableComponent>(virtItem2.Value);
    }

    private void FreeHands(EntityUid uid, EntityUid mech)
    {
        _virtualItem.DeleteInHandsMatching(uid, mech);
    }

    // Forge-Change Change End

    private void OnGetMeleeWeapon(EntityUid uid, MechPilotComponent component, GetMeleeWeaponEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<MechComponent>(component.Mech, out var mech))
            return;

        var weapon = mech.CurrentSelectedEquipment ?? component.Mech;
        args.Weapon = weapon;
        args.Handled = true;
    }

    private void OnCanAttackFromContainer(EntityUid uid, MechPilotComponent component, CanAttackFromContainerEvent args)
    {
        args.CanAttack = true;
    }

    private void OnAttackAttempt(EntityUid uid, MechPilotComponent component, AttackAttemptEvent args)
    {
        if (args.Target == component.Mech)
            args.Cancel();
    }

    // Forge-Change: Prevent guns being used out of mechs if CCVAR is set.
    private void OnShotAttempted(EntityUid uid, MechEquipmentComponent component, ref ShotAttemptedEvent args)
    {
        if (!component.EquipmentOwner.HasValue
            || !HasComp<MechComponent>(component.EquipmentOwner.Value))
        {
            if (!_canUseMechGunOutside)
                args.Cancel();
            return;
        }

        var ev = new HandleMechEquipmentBatteryEvent();
        RaiseLocalEvent(uid, ev);
    }

    public void UpdateAppearance(EntityUid uid, MechComponent? component = null,
        AppearanceComponent? appearance = null)
    {
        if (!Resolve(uid, ref component, ref appearance, false))
            return;

        _appearance.SetData(uid, MechVisuals.Open, IsEmpty(component), appearance);
        _appearance.SetData(uid, MechVisuals.Broken, component.Broken, appearance);
        _appearance.SetData(uid, MechVisuals.Light, component.Light, appearance); // Forge-Change
    }

    private void OnDragDrop(EntityUid uid, MechComponent component, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var doAfterEventArgs = new DoAfterArgs(EntityManager, args.Dragged, component.EntryDelay, new MechEntryEvent(), uid, target: uid)
        {
            BreakOnMove = true,
        };

        _doAfter.TryStartDoAfter(doAfterEventArgs);
    }

    private void OnCanDragDrop(EntityUid uid, MechComponent component, ref CanDropTargetEvent args)
    {
        args.Handled = true;

        args.CanDrop |= !component.Broken && CanInsert(uid, args.Dragged, component);
    }

    private void OnEmagged(EntityUid uid, MechComponent component, ref GotEmaggedEvent args) // Forge-Change
    {
        if (!component.BreakOnEmag)
            return;
        args.Handled = true;
        component.EquipmentWhitelist = null;
        Dirty(uid, component);
    }

    // Frontier
    private void RaiseEquipmentEquippedEvent(Entity<MechComponent> ent, EntityUid? pilot = null)
    {
        if (_net.IsServer && ent.Comp.CurrentSelectedEquipment != null)
        {
            var ev = new MechEquipmentEquippedAction
            {
                Mech = ent,
                Pilot = pilot ?? ent.Comp.PilotSlot.ContainedEntity
            };
            RaiseLocalEvent(ent.Comp.CurrentSelectedEquipment.Value, ev);
        }
    }
    // End Frontier
}

/// <summary>
///     Event raised when the battery is successfully removed from the mech,
///     on both success and failure
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RemoveBatteryEvent : SimpleDoAfterEvent
{
}

/// <summary>
///     Forge-Change: Event raised when the gas tank is successfully removed from the mech,
///     on both success and failure
/// </summary>
[Serializable, NetSerializable]
public sealed partial class RemoveGasTankEvent : SimpleDoAfterEvent
{
}

/// <summary>
///     Event raised when a person removes someone from a mech,
///     on both success and failure
/// </summary>
[Serializable, NetSerializable]
public sealed partial class MechExitEvent : SimpleDoAfterEvent
{
}

/// <summary>
///     Event raised when a person enters a mech, on both success and failure
/// </summary>
[Serializable, NetSerializable]
public sealed partial class MechEntryEvent : SimpleDoAfterEvent
{
}

/// <summary>
///     Forge-Change: Event raised when an user attempts to fire a mech weapon to check if its battery is drained
/// </summary>

[Serializable, NetSerializable]
public sealed partial class HandleMechEquipmentBatteryEvent : EntityEventArgs
{
}

public sealed partial class MechToggleThrustersEvent : InstantActionEvent // Forge-Change
{
}