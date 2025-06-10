using Content.Shared._Corvax.Skeleton;
using Content.Shared.Body.Part;
using Content.Shared.Mobs;
using Content.Shared.Inventory;
using Content.Server.Mind;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server._Corvax.Skeleton;

public sealed class SkeletonCollapseSystem : EntitySystem
{
    [Dependency] private readonly TransformSystem _transform = null!;
    [Dependency] private readonly IEntityManager _entMan = null!;
    [Dependency] private readonly MindSystem _mindSystem = null!;
    [Dependency] private readonly MetaDataSystem _metaSystem = null!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BaseMobSkeletonPersonComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(EntityUid uid, BaseMobSkeletonPersonComponent _, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;

        CollapseToSkull(uid);
    }

    private void CollapseToSkull(EntityUid uid)
    {
        // 1) Дропаем инвентарь
        if (_entMan.TryGetComponent(uid, out InventoryComponent? inventory))
        {
            var dropCoords = new EntityCoordinates(uid, _transform.GetWorldPosition(uid));
            var slotEnum = new InventorySystem.InventorySlotEnumerator(inventory);
            while (slotEnum.NextItem(out var item))
            {
                if (!_entMan.Deleted(item) && _entMan.HasComponent<TransformComponent>(item))
                {
                    _transform.SetCoordinates(item, dropCoords);
                }
            }
        }

        // 2) Удаляем все части тела, кроме головы
        var parts = _entMan.EntityQueryEnumerator<BodyPartComponent>();
        while (parts.MoveNext(out var partUid, out var partComp))
        {
            if (partComp.Body == uid && partComp.PartType != BodyPartType.Head)
                _entMan.DeleteEntity(partUid);
        }

        // 3) Спавним череп
        var worldPos = _transform.GetWorldPosition(uid);
        var skull = _entMan.SpawnEntity("HeadSkeleton", new EntityCoordinates(uid, worldPos));

        // 4) Переносим ум
        // 4) Переносим ум
        if (_entMan.TryGetComponent<MindContainerComponent>(uid, out var container) &&
            container.Mind is { } mindUid &&  // проверяем, что Mind не null
            _entMan.TryGetComponent<MindComponent>(mindUid, out var mindComp))
        {
            if (_entMan.TryGetComponent<MindContainerComponent>(skull, out _))
            {
                _mindSystem.TransferTo(mindUid, skull, mind: mindComp);
            }
            else
            {
                Logger.WarningS("skeleton", $"Череп {ToPrettyString(skull)} не имеет MindContainerComponent");
            }
        }
        else
        {
            Logger.WarningS("skeleton", $"Не удалось получить Mind с {ToPrettyString(uid)} для переноса");
        }


        // 5) Копируем имя и описание
        if (_entMan.TryGetComponent(uid, out MetaDataComponent? oldMeta))
        {
            var skullMeta = _entMan.EnsureComponent<MetaDataComponent>(skull);
            _metaSystem.SetEntityName(skull, oldMeta.EntityName);
            _metaSystem.SetEntityDescription(skull, oldMeta.EntityDescription);
        }

        // 6) Помечаем оригинал для воскрешения
        var skullComp = _entMan.GetComponent<SkeletonSkullComponent>(skull);
        skullComp.OriginalBody = uid;

        // 7) Закрепляем тело на карте
        _transform.AttachToGridOrMap(uid);
    }
}
