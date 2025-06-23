using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared._Corvax.Skeleton;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Robust.Server.Containers;
using Robust.Server.GameObjects;

namespace Content.Server._Corvax.Skeleton;

public sealed class SkeletonReformSystem : EntitySystem
{
    [Dependency] private readonly SharedBankSystem _bank = default!;
    [Dependency] private readonly ContainerSystem _cont = default!;
    [Dependency] private readonly DamageableSystem _dmg = default!;
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MobStateSystem _state = default!;
    [Dependency] private readonly TransformSystem _xform = default!;


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

        if (!_cont.Remove(body, pocket, true, true))
        {
            _popup.PopupEntity("Не удалось извлечь тело!", skull, skull);
            return;
        }

        if (_ent.TryGetComponent<MetaDataComponent>(body, out var meta) &&
            _ent.TryGetComponent<TransformComponent>(body, out var xform))
        {
            var entity = new Entity<TransformComponent?, MetaDataComponent?>(body, xform, meta);
            while (_cont.TryGetContainingContainer(entity, out var containing))
            {
                _cont.RemoveEntity(containing.Owner, body);
            }
        }

        var pos = _ent.GetComponent<TransformComponent>(skull).Coordinates;
        _xform.SetCoordinates(body, pos);

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
