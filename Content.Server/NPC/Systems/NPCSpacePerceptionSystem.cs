using Content.Server.Npc.Components;
using Content.Shared.Mobs.Components;
using Content.Server.Shuttles.Components;
using Content.Shared.Physics;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using System.Linq;

namespace Content.Server.Npc.Systems
{
    public sealed class SpaceNpcPerceptionSystem : EntitySystem
    {
        [Dependency] private readonly EntityLookupSystem _lookup = default!;
        [Dependency] private readonly SharedPhysicsSystem _physics = default!;
        [Dependency] private readonly SharedTransformSystem _transform = default!;
        [Dependency] private readonly IRobustRandom _random = default!;

        private const int VisionMask = (int)(CollisionGroup.Opaque | CollisionGroup.Impassable | CollisionGroup.MapGrid);

        // Максимальное количество обновлений НПС за один тик сервера.
        // Если НПС больше этого числа, остальные подождут следующего тика.
        private const int MaxUpdatesPerTick = 10;

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            int updatesThisTick = 0;

            // Используем QueryEnumerator для скорости
            var query = EntityQueryEnumerator<SpaceNpcComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var npc, out var transform))
            {
                // ОПТИМИЗАЦИЯ 1: Разделение нагрузки.
                // При инициализации мы даем случайное смещение таймеру, 
                // чтобы все НПС не обновлялись в одну секунду.
                // В Update мы просто копим время.
                npc.ScanAccumulator += frameTime;

                // Если время не пришло - скипаем
                if (npc.ScanAccumulator < npc.ScanFrequency) continue;

                // ОПТИМИЗАЦИЯ 2: Лимит обновлений на тик.
                // Если мы уже обновили 10 НПС в этом кадре, остальных откладываем.
                if (updatesThisTick >= MaxUpdatesPerTick) break;

                // Сбрасываем таймер и добавляем немного рандома, чтобы рассинхронизировать их снова
                npc.ScanAccumulator = 0 - _random.NextFloat(0, 0.2f);
                updatesThisTick++;

                UpdateTarget(uid, npc, transform);
            }
        }

        private void UpdateTarget(EntityUid uid, SpaceNpcComponent npc, TransformComponent transform)
        {
            var mapPos = _transform.GetMapCoordinates(uid, transform);

            // ОПТИМИЗАЦИЯ 3: Проверка текущей цели.
            // Если цель уже есть, она жива и мы её всё ещё видим - НЕ НАДО искать новую.
            // Это экономит кучу ресурсов.
            if (npc.CurrentTarget != null && Exists(npc.CurrentTarget) && !Terminating(npc.CurrentTarget.Value))
            {
                var targetPos = _transform.GetMapCoordinates(npc.CurrentTarget.Value);
                float dist = (targetPos.Position - mapPos.Position).Length();

                // Если цель всё еще в радиусе и видна
                if (dist <= npc.VisionRadius && CanSee(mapPos, targetPos, uid, npc.CurrentTarget.Value))
                {
                    // Оставляем старую цель, выходим.
                    return;
                }
            }

            // Если цели нет или она потеряна - ищем новую (дорогостоящая операция)
            FindNewTarget(uid, npc, mapPos);
        }

        private void FindNewTarget(EntityUid uid, SpaceNpcComponent npc, MapCoordinates mapPos)
        {
            EntityUid? bestTarget = null;
            float closestDist = float.MaxValue;

            // ОПТИМИЗАЦИЯ 4: GetEntitiesInRange аллоцирует List/HashSet. 
            // Это нагружает GC (сборщик мусора).
            // В идеале использовать GetEntitiesInRange(..., HashSet<EntityUid> buffer), 
            // но для простоты оставим так, ограничив частоту вызовов выше.
            var entities = _lookup.GetEntitiesInRange(mapPos, npc.VisionRadius);

            foreach (var target in entities)
            {
                if (target == uid) continue;

                // Быстрые проверки (без математики и физики)
                if (!HasComp<MobStateComponent>(target) && !HasComp<ShuttleComponent>(target)) continue;

                // Проверка карты (очень быстрая)
                var targetXform = Transform(target);
                if (targetXform.MapID != mapPos.MapId) continue;

                // Проверка дистанции (квадратная дистанция быстрее, чем корень, но Length() удобнее)
                var targetPos = _transform.GetMapCoordinates(target, targetXform);
                float dist = (targetPos.Position - mapPos.Position).Length();

                if (dist > npc.VisionRadius) continue;
                if (dist >= closestDist) continue; // Уже нашли кого-то ближе

                // Самая дорогая проверка - Raycast - только если кандидат реально ближе
                if (!CanSee(mapPos, targetPos, uid, target)) continue;

                closestDist = dist;
                bestTarget = target;
            }

            npc.CurrentTarget = bestTarget;
        }

        private bool CanSee(MapCoordinates origin, MapCoordinates target, EntityUid sourceEnt, EntityUid targetEnt)
        {
            var direction = target.Position - origin.Position;
            var distance = direction.Length();

            var ray = new CollisionRay(origin.Position, direction.Normalized(), VisionMask);
            var rayResults = _physics.IntersectRay(origin.MapId, ray, distance, returnOnFirstHit: true);

            foreach (var result in rayResults)
            {
                if (result.HitEntity == targetEnt) return true;
                if (result.HitEntity == sourceEnt) continue;
                return false;
            }
            return true;
        }
    }
}
