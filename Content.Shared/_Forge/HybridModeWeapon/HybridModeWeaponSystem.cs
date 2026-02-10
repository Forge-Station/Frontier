using Content.Shared._Forge.FireModes;
using Content.Shared.Access.Systems;
using Content.Shared.Body.Systems;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Toolshed.TypeParsers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Content.Shared._Forge.TripleModeWeapon
{
    
    public sealed class HybridModeWeaponSystem : EntitySystem
    {
        protected const string ChamberSlot = "gun_chamber";

        [Dependency] private readonly SharedGunSystem _sharedGunSystem = default!;
        [Dependency] private readonly AccessReaderSystem _accessReaderSystem = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
        [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
        [Dependency] private readonly SharedAppearanceSystem _appearanceSystem = default!;
        [Dependency] private readonly IComponentFactory _factory = default!;
        [Dependency] protected readonly SharedContainerSystem _containerSystem = default!;


        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<HybridModeWeaponComponent, UseInHandEvent>(OnUseInHand);
            SubscribeLocalEvent<HybridModeWeaponComponent, GetVerbsEvent<Verb>>(OnGetVerb);
            SubscribeLocalEvent<HybridModeWeaponComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<HybridAmmoProviderComponent, GetAmmoCountEvent>(OnAmmoCount);

        }

        private void OnAmmoCount(EntityUid uid, HybridAmmoProviderComponent component, ref GetAmmoCountEvent args)
        {
            args.Count = component.Shots;
            args.Capacity = component.Capacity;
        }

        private ProtoId<EntityPrototype> GetMode(HybridModeWeaponComponent component)
        {
            return component.FireModes.ElementAt(component.CurrentFireMode).Key;
        }


        private void OnExamined(EntityUid uid, HybridModeWeaponComponent component, ExaminedEvent args)
        {
            if (component.FireModes.Count < 2)
                return;

            var fireMode = GetMode(component);

            HitscanPrototype? hitscanPrototype = null;

            if (!_prototypeManager.TryIndex<EntityPrototype>(fireMode, out var prototype) && !_prototypeManager.TryIndex<HitscanPrototype>(fireMode, out hitscanPrototype))
                return;

            if (prototype is not null)
                args.PushMarkup(Loc.GetString("gun-set-fire-mode", ("mode", prototype.Name)));

            if (hitscanPrototype is not null)
                args.PushMarkup("Лазер");
        }

        private void OnGetVerb(EntityUid uid, HybridModeWeaponComponent component, GetVerbsEvent<Verb> args)
        {
            if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
                return;

            if (component.FireModes.Count < 2)
                return;

            if (!_accessReaderSystem.IsAllowed(args.User, uid))
                return;

            for (var i = 0; i < component.FireModes.Count; i++)
            {
                var fireMode = component.FireModes.ElementAt(i);
                EntityPrototype? entProto = null;
                HitscanPrototype? hitProto = null;
                if (_prototypeManager.TryIndex<EntityPrototype>(fireMode.Key, out var prototype))
                {
                    entProto = prototype;
                }

                if (_prototypeManager.TryIndex<HitscanPrototype>(fireMode.Key, out var hitscanPrototype))
                {
                    hitProto = hitscanPrototype;
                }
                var index = i;

                if (hitProto is null && entProto is null)
                    return;

                var v = new Verb
                {
                    Priority = 1,
                    Category = VerbCategory.SelectType,
                    Text = entProto?.Name ?? "Лазер",
                    Disabled = i == component.CurrentFireMode,
                    Impact = LogImpact.Medium,
                    DoContactInteraction = true,
                    Act = () =>
                    {
                        TrySetFireMode(uid, component, index, args.User);
                    }
                };

                args.Verbs.Add(v);
            }
        }

        private void OnUseInHand(EntityUid uid, HybridModeWeaponComponent component, ref UseInHandEvent args)
        {
            if (args.Handled)
                return;

            args.Handled = true;
            TryCycleFireMode(uid, component, args.User);
        }

        public void TryCycleFireMode(EntityUid uid, HybridModeWeaponComponent component, EntityUid? user = null)
        {
            if (component.FireModes.Count < 2)
                return;

            var index = (component.CurrentFireMode + 1) % component.FireModes.Count;
            TrySetFireMode(uid, component, index, user);
        }

        public bool TrySetFireMode(EntityUid uid, HybridModeWeaponComponent component, int index, EntityUid? user = null)
        {
            if (index < 0 || index >= component.FireModes.Count)
                return false;

            if (user != null && !_accessReaderSystem.IsAllowed(user.Value, uid))
                return false;

            SetFireMode(uid, component, index, user);

            return true;
        }

        private void SetFireMode(EntityUid uid, HybridModeWeaponComponent component, int index, EntityUid? user = null)
        {
            component.CurrentFireMode = index;
            var fireMode = component.FireModes.ElementAt(index);
            var fireModeName = fireMode.Value;
            EntityPrototype? prototype;
            GunComponent? gunComponent = null;

            if (_prototypeManager.TryIndex<EntityPrototype>(fireMode.Key, out prototype))
            {
                if (TryComp<AppearanceComponent>(uid, out var appearance))
                    _appearanceSystem.SetData(uid, BatteryWeaponFireModeVisuals.State, prototype.ID, appearance);

                if (user != null)
                    _popupSystem.PopupClient(Loc.GetString("gun-set-fire-mode", ("mode", prototype.Name)), uid, user.Value);
            }

            if (_prototypeManager.TryIndex<HitscanPrototype>(fireMode.Key, out var hitscanPrototype))
            {
                if (TryComp<AppearanceComponent>(uid, out var appearance))
                    _appearanceSystem.SetData(uid, BatteryWeaponFireModeVisuals.State, hitscanPrototype.ID, appearance);

                if (user != null)
                    _popupSystem.PopupClient("Лазер", uid, user.Value);
            }

            if (TryComp<HitscanBatteryAmmoProviderComponent>(uid, out var hitscanAmmoProvider))
                RemComp<HitscanBatteryAmmoProviderComponent>(uid);

            if (TryComp<ChamberMagazineAmmoProviderComponent>(uid, out var magazineAmmoProvider))
                RemComp<ChamberMagazineAmmoProviderComponent>(uid);
                

            if (TryComp<HybridAmmoProviderComponent>(uid, out var hybridAmmoProviderComponent))
                RemComp<HybridAmmoProviderComponent>(uid);
                

            switch (fireModeName)
            {
                case "battery":
                    if (hitscanPrototype is not null)
                    {
                        var hitscanComponent = (HitscanBatteryAmmoProviderComponent)_factory.GetComponent(typeof(HitscanBatteryAmmoProviderComponent));

                        hitscanComponent.NetSyncEnabled = false;
                        hitscanComponent.Prototype = hitscanPrototype.ID;
                        AddComp(uid, hitscanComponent);
                        Dirty(uid, hitscanComponent);
                    }
                        
                    break;
                case "ammo":
                    AddComp<ChamberMagazineAmmoProviderComponent>(uid);
                    break;
                case "hybrid":
                    AddComp<HybridAmmoProviderComponent>(uid);
                    break;
            }

            Dirty(uid, component);

            var updateClientAmmoEvent = new UpdateClientAmmoEvent();

            RaiseLocalEvent(uid, ref updateClientAmmoEvent);
        }
    }
}
