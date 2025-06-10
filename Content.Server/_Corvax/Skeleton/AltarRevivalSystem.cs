using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Interaction;
using Content.Shared.Item;
using Content.Shared.Mobs.Components;
using Content.Shared._Corvax.Skeleton;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Robust.Server.GameObjects;

namespace Content.Server._Corvax.Skeleton;

/// <summary>
/// Воскрешает скелета, если на алтарь положен его череп и нужное количество монет.
/// </summary>
public sealed class AltarRevivalSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = null!;
    [Dependency] private readonly IEntityManager _entMan = null!;
    [Dependency] private readonly TransformSystem _xform = null!;
    [Dependency] private readonly DamageableSystem _damageable = null!;
    [Dependency] private readonly MindSystem _mindSystem = null!;

    public override void Initialize()
    {
        SubscribeLocalEvent<AltarRevivalComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(EntityUid altarUid, AltarRevivalComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || args.Target is not { } skullUid || skullUid == altarUid)
            return;

        // Проверка: правильный череп
        if (!_entMan.TryGetComponent<MetaDataComponent>(skullUid, out var skullMeta) ||
            skullMeta.EntityPrototype?.ID != comp.SkullPrototype ||
            !_entMan.TryGetComponent<SkeletonSkullComponent>(skullUid, out var skullComp))
        {
            _popup.PopupEntity("Этот череп не содержит душу.", altarUid, args.User);
            return;
        }

        // Проверка: есть оригинальное тело
        if (skullComp.OriginalBody is not { } skeletonUid ||
            !_entMan.EntityExists(skeletonUid) ||
            !_entMan.HasComponent<MobStateComponent>(skeletonUid))
        {
            _popup.PopupEntity("Невозможно найти тело скелета.", altarUid, args.User);
            return;
        }

        // Проверка координат
        var altarCoords = _xform.GetMapCoordinates(altarUid);
        if (_xform.GetMapCoordinates(skullUid) != altarCoords)
            return;

        // Поиск монет
        var coins = new List<EntityUid>();
        var query = _entMan.EntityQueryEnumerator<ItemComponent>();
        while (query.MoveNext(out var itemUid, out _))
        {
            if (!_entMan.TryGetComponent(itemUid, out MetaDataComponent? meta))
                continue;

            if (meta.EntityPrototype?.ID != comp.RequiredItemPrototype)
                continue;

            if (_xform.GetMapCoordinates(itemUid) != altarCoords)
                continue;

            coins.Add(itemUid);
            if (coins.Count >= comp.RequiredItemCount)
                break;
        }

        if (coins.Count < comp.RequiredItemCount)
        {
            _popup.PopupEntity("Не хватает монет для воскрешения.", altarUid, args.User);
            return;
        }

        // Получение MindComponent перед удалением черепа
        MindComponent? mindComp;
        if (_entMan.TryGetComponent<MindContainerComponent>(skullUid, out var mindContainer) &&
            mindContainer.HasMind &&
            _entMan.TryGetComponent(mindContainer.Mind.Value, out mindComp)) // ✅ без типа
        {
            _mindSystem.TransferTo(mindContainer.Mind.Value, skeletonUid, mind: mindComp);
        }


        // Удаление черепа и монет
        _entMan.DeleteEntity(skullUid);
        foreach (var coin in coins)
        {
            _entMan.DeleteEntity(coin);
        }

        // Сброс урона
        if (_entMan.TryGetComponent(skeletonUid, out DamageableComponent? damageable))
        {
            _damageable.SetAllDamage(skeletonUid, damageable, FixedPoint2.Zero);
        }

        // Возвращение на карту
        _xform.AttachToGridOrMap(skeletonUid);
        _xform.SetWorldPosition(skeletonUid, _xform.GetWorldPosition(altarUid));

        _popup.PopupEntity("Скелет восстал из мёртвых!", altarUid, args.User);
        args.Handled = true;
    }
}
