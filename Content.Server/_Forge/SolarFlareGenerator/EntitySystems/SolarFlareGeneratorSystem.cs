using Content.Client;
using Content.Server.Bed.Components;
using Content.Server.Botany.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Server.Radio;
using Content.Shared.Bed.Sleep;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Systems;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell.Components;
using Content.Shared.Radio.Components;
using Content.Shared.Radio.EntitySystems;
using JetBrains.FormatRipper.Elf;
using Robust.Shared.GameObjects;
using Robust.Shared.Timing;
using System;
using System.ComponentModel;


namespace Content.Server.Solar.EntitySystems
{
    public sealed class SolarFlareGeneratorSystem : EntitySystem
    {
        [Dependency] private readonly PowerCellSystem _powerCell = default!;
        [Dependency] private readonly BatterySystem _battery = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly SharedDeviceNetworkJammerSystem _jammer = default!;
        [Dependency] private readonly SharedJammerSystem _jammerSystem = default!;
        [Dependency] private readonly IGameTiming _timing = default!;
        [Dependency] protected readonly SharedPopupSystem Popup = default!;
        private const float _cooldownTimerValue = 15;
        private const float _effectTimer = 30;
        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SolarFlareGeneratorComponent, InteractHandEvent>(OnInteractHand);
            SubscribeLocalEvent<RadioReceiveAttemptEvent>(OnRadioReceiveAttempt);
        }

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            var frameTimeSpan = TimeSpan.FromSeconds(frameTime);
            var query = EntityQueryEnumerator<SolarFlareGeneratorComponent>();

            while (query.MoveNext(out var uid, out var solarFlareComponent))
            {
                if (solarFlareComponent.IsActive)
                {
                    UpdateActiveGenerator(solarFlareComponent, frameTimeSpan);
                }
                else if (!solarFlareComponent.IsReady)
                {
                    UpdateCooldown(solarFlareComponent, frameTimeSpan);
                }
                else
                {
                    solarFlareComponent.EffectTimer = TimeSpan.Zero;
                }
            }
        }

        private void UpdateActiveGenerator(SolarFlareGeneratorComponent component, TimeSpan frameTime)
        {
            // Обновление таймера эффекта
            if (component.EffectTimer > TimeSpan.Zero)
            {
                component.EffectTimer -= frameTime;

                if (component.EffectTimer <= TimeSpan.Zero)
                {
                    component.IsActive = false;
                    component.EffectTimer = TimeSpan.Zero;
                }
            }

            // Обновление кулдауна 
            UpdateCooldown(component, frameTime);
        }

        private void UpdateCooldown(SolarFlareGeneratorComponent component, TimeSpan frameTime)
        {
            if (component.CooldownTimer > TimeSpan.Zero)
            {
                component.CooldownTimer -= frameTime;
            }
            else if (!component.IsReady)
            {
                component.IsReady = true;
                component.CooldownTimer = TimeSpan.Zero;
            }
        }

        private void OnInteractHand(Entity<SolarFlareGeneratorComponent> entity, ref InteractHandEvent args)
        {
            if (args.Handled)
                return;

            if (entity.Comp.IsReady)
            {
                var state = Loc.GetString("radio-jammer-component-on-state");
                var message = Loc.GetString("radio-jammer-component-on-use", ("state", state));
                Popup.PopupEntity(message, args.User, args.User);
                OnActivate(entity, args);
            }

        }

        private void OnActivate(Entity<SolarFlareGeneratorComponent> entity, InteractHandEvent args)
        {
            StartSolarFlareGenerator(entity.Comp);
            args.Handled = true;
        }

        private void StartSolarFlareGenerator(SolarFlareGeneratorComponent component)
        {
            component.IsActive = true;
            component.IsReady = false;
            component.CooldownTimer = TimeSpan.FromMinutes(_cooldownTimerValue);
            component.EffectTimer = TimeSpan.FromSeconds(_effectTimer);
        }

        private void OnRadioReceiveAttempt(ref RadioReceiveAttemptEvent args)
        {
            if (ShouldCancelReceive(args.RadioSource))
            {
                args.Cancelled = true;
            }

        }

        private bool ShouldCancelReceive(EntityUid sourceUid)
        {
            
            var query = EntityQueryEnumerator<TransformComponent, SolarFlareGeneratorComponent>();

            while (query.MoveNext(out var uid, out var transform, out var solarFlareGeneratorComponent))
            {
                var source = Transform(sourceUid).Coordinates;
                if (_transform.InRange(source, transform.Coordinates, solarFlareGeneratorComponent.Radius) && solarFlareGeneratorComponent.IsActive)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
