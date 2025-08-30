using Content.Server._NF.Bank;
using Content.Server.Store.Components;
using Content.Shared._Forge.SoulContract;
using Content.Shared.Body.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Gibbing.Systems;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using static Content.Server.Power.Pow3r.PowerState;

namespace Content.Server._Forge.SoulContractSystem
{
    public sealed class SoulContractSystem : EntitySystem
    {
        [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
        [Dependency] private readonly BankSystem _bank = default!;
        [Dependency] private readonly InventorySystem _inventory = default!;
        [Dependency] private readonly SharedBodySystem _bodySystem = default!;

        public override void Initialize()
        {
            base.Initialize();

            SubscribeLocalEvent<SoulContractComponent, UseInHandEvent>(OnUseInHand);
            SubscribeLocalEvent<SoulContractComponent, SoulContractDoAfterEvent>(OnDoAfter);
        }

        private void OnUseInHand(Entity<SoulContractComponent> entity, ref UseInHandEvent args)
        {
            var (uid, soulContract) = entity;

            var user = args.User;

            var doAfterArgs = new DoAfterArgs(EntityManager,
                user,
                soulContract.UseTime,
                new SoulContractDoAfterEvent(),
                eventTarget: uid)
            {
                BreakOnHandChange = false,
                BreakOnMove = true,
                BreakOnDamage = true,
                MovementThreshold = 0.01f,
                DistanceThreshold = 5,
                NeedHand = true
            };

            _doAfterSystem.TryStartDoAfter(doAfterArgs);
        }

        private void OnDoAfter(Entity<SoulContractComponent> entity, ref SoulContractDoAfterEvent args)
        {
            if (args.Cancelled)
                return;

            Del(entity.Owner);
            args.Handled = _bank.TryBankDeposit(args.Args.User, entity.Comp.Reward, true) && TryGib(entity, args.Args.User);
        }

        private bool TryGib(Entity<SoulContractComponent> entity, EntityUid user)
        {
            var gibHashSet = _bodySystem.GibBody(user);

            return gibHashSet.Count > 0;
        }
    }
}
