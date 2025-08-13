///TAKEN FROM: https://github.com/Workbench-Team/space-station-14/pull/327

using Content.Server.Materials;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.PowerCell;
using Content.Shared.Materials;
using Content.Shared.PowerCell.Components;

namespace Content.Server._ADT.PlasmaCutter
{

    /// <summary>
    /// This CODE FULL OF SHICODE!!!
    /// <see cref="_ADT.PlasmaCutter.BatteryRechargeComponent"/>
    /// </summary>
    public sealed class BatteryRechargeSystem : EntitySystem
    {
        [Dependency] private readonly MaterialStorageSystem _materialStorage = default!;
        [Dependency] private readonly BatterySystem _batterySystem = default!;
        [Dependency] private readonly PowerCellSystem _powerCell = default!;


        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<MaterialStorageComponent, MaterialEntityInsertedEvent>(OnMaterialAmountChanged);
            SubscribeLocalEvent<_ADT.PlasmaCutter.BatteryRechargeComponent, ChargeChangedEvent>(OnChargeChanged);
        }

        private void OnMaterialAmountChanged(EntityUid uid, MaterialStorageComponent component, ref MaterialEntityInsertedEvent args)
        {
            if (component.MaterialWhiteList != null)
                if (TryComp<_ADT.PlasmaCutter.BatteryRechargeComponent>(uid, out var comp))
                    foreach (var fuelType in component.MaterialWhiteList)
                    {
                        FuelAddCharge(uid, fuelType, comp);
                    }
        }

        private void OnChargeChanged(EntityUid uid, _ADT.PlasmaCutter.BatteryRechargeComponent component, ChargeChangedEvent args)
        {
            ChangeStorageLimit(uid, component.StorageMaxCapacity);
        }

        private void ChangeStorageLimit(
            EntityUid uid,
            int value,
            BatteryComponent? battery = null)
        {
            if (!Resolve(uid, ref battery) && !HasComp<PowerCellSlotComponent>(uid))
                return;
            if (battery != null && battery.CurrentCharge == battery.MaxCharge)
                value = 0;
            _materialStorage.TryChangeStorageLimit(uid, value);
        }

        private void FuelAddCharge(
            EntityUid uid,
            string fuelType,
            _ADT.PlasmaCutter.BatteryRechargeComponent recharge)
        {
            var availableMaterial = _materialStorage.GetMaterialAmount(uid, fuelType);
            var chargePerMaterial = availableMaterial * recharge.Multiplier;
            if (TryComp<PowerCellSlotComponent>(uid, out var slot) && _powerCell.TryGetBatteryFromSlot(uid, out var batteryEnt, out var battery, slot))
            {
                _batterySystem.TryAddCharge(batteryEnt.Value, chargePerMaterial, battery);
            }
            if (_materialStorage.TryChangeMaterialAmount(uid, fuelType, -availableMaterial))
            {
                _batterySystem.TryAddCharge(uid, chargePerMaterial);
            }
        }
    }

}
