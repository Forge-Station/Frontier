using Content.Server.Emp;
using Content.Shared.Damage;
using Content.Shared.Emp;
using Robust.Shared.Random;
using Content.Shared.Mech;
using Content.Shared.Mech.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Construction;

namespace Content.Server.Mech.Systems
{
    public sealed class MechEmpSystem : EntitySystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly DamageableSystem _damageable = default!;

        public override void Initialize()
        {
            base.Initialize();
            SubscribeLocalEvent<MechComponent, EmpPulseEvent>(OnEmpPulse);
        }

        private void OnEmpPulse(EntityUid uid, MechComponent comp, ref EmpPulseEvent args)
        {
            var (min, max) = GetEmpDamageRange(comp);
            var damage = _random.Next(min, max + 1);

            var spec = new DamageSpecifier();
            spec.DamageDict.Add("Shock", damage);

            _damageable.TryChangeDamage(uid, spec, true);
        }

        private (int, int) GetEmpDamageRange(MechComponent comp)
        {
            var min = 20;
            var max = 40;

            var highestRating = 0;

            if (comp.CapacitorSlot?.ContainedEntity is { } capacitorUid &&
                TryComp<MachinePartComponent>(capacitorUid, out var capacitor))
            {
                highestRating = capacitor.Rating;
            }

            switch (highestRating)
            {
                case 1:
                    min = 250; max = 300;
                    break;
                case 2:
                    min = 85;  max = 100;
                    break;
                case 3:
                    min = 50;  max = 60;
                    break;
                case 4:
                    min = 25;  max = 30;
                    break;
            }

            return (min, max);
        }
    }
}