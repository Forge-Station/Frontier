using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared._Forge.Skeleton;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Server.Containers;

namespace Content.Server._Forge.Skeleton;

public sealed class SkeletonReformSystem : EntitySystem
{
    [Dependency] private readonly SharedBankSystem _bank = default!;
    [Dependency] private readonly ContainerSystem _cont = default!;
    [Dependency] private readonly DamageableSystem _dmg = default!;
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _state = default!;


    public override void Initialize()
    {
        SubscribeLocalEvent<SkeletonReformComponent, SkeletonReformEvent>(OnReform);
    }

    private void OnReform(EntityUid skull, SkeletonReformComponent comp, SkeletonReformEvent args)
    {
        if (comp.OriginalBody is not { } body)
            return;

        if (!_cont.TryGetContainer(skull, "SkeletonBody", out var pocket) || !pocket.Contains(body))
        {
            _popup.PopupEntity("Тело не найдено!", skull, skull);
            return;
        }

        if (!_ent.TryGetComponent(body, out TransformComponent? bodyXform) ||
            !_ent.TryGetComponent(body, out MetaDataComponent? bodyMeta))
            return;

        var eBody = new Entity<TransformComponent?, MetaDataComponent?>(body, bodyXform, bodyMeta);
        while (_cont.TryGetContainingContainer(eBody, out var container))
        {
            _cont.Remove(eBody, container, true, true);
        }

        if (_ent.TryGetComponent(body, out DamageableComponent? dmg))
            _dmg.SetAllDamage(body, dmg, FixedPoint2.Zero);

        _state.ChangeMobState(body, MobState.Alive);

        if (_mind.TryGetMind(skull, out var mindUid, out _))
            _mind.TransferTo(mindUid, body);

        if (_ent.TryGetComponent(skull, out BankAccountComponent? bank))
            _bank.SetBalance(body, bank.Balance);

        var txt = string.IsNullOrWhiteSpace(comp.PopupText)
            ? "Скелет восстал!"
            : Loc.GetString(comp.PopupText, ("name", body));
        _popup.PopupEntity(txt, body, skull);

        QueueDel(skull);
    }
}
