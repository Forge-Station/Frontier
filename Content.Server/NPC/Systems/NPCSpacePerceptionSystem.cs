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

        private const int MaxUpdatesPerTick = 10;

        public override void Update(float frameTime)
        {
            base.Update(frameTime);

            int updatesThisTick = 0;

            var query = EntityQueryEnumerator<SpaceNpcComponent, TransformComponent>();
            while (query.MoveNext(out var uid, out var npc, out var transform))
            {
                npc.ScanAccumulator += frameTime;

                if (npc.ScanAccumulator < npc.ScanFrequency)
                {
                    continue;
                }
                if (updatesThisTick >= MaxUpdatesPerTick)
                {
                    break;
                }
                npc.ScanAccumulator = 0 - _random.NextFloat(0, 0.2f);
                updatesThisTick++;

                UpdateTarget(uid, npc, transform);
            }
        }

        private void UpdateTarget(EntityUid uid, SpaceNpcComponent npc, TransformComponent transform)
        {
            var mapPos = _transform.GetMapCoordinates(uid, transform);

            if (npc.CurrentTarget != null && Exists(npc.CurrentTarget) && !Terminating(npc.CurrentTarget.Value))
            {
                var targetPos = _transform.GetMapCoordinates(npc.CurrentTarget.Value);
                float dist = (targetPos.Position - mapPos.Position).Length();

                if (dist <= npc.VisionRadius && CanSee(mapPos, targetPos, uid, npc.CurrentTarget.Value))
                {
                    return;
                }
            }

            FindNewTarget(uid, npc, mapPos);
        }

        private void FindNewTarget(EntityUid uid, SpaceNpcComponent npc, MapCoordinates mapPos)
        {
            EntityUid? bestTarget = null;
            float closestDist = float.MaxValue;

            var entities = _lookup.GetEntitiesInRange(mapPos, npc.VisionRadius);

            foreach (var target in entities)
            {
                if (target == uid)
                {
                    continue;
                }
                if (!HasComp<MobStateComponent>(target) && !HasComp<ShuttleComponent>(target))
                {
                    continue;
                }

                var targetXform = Transform(target);
                if (targetXform.MapID != mapPos.MapId)
                {
                    continue;
                }
                var targetPos = _transform.GetMapCoordinates(target, targetXform);
                float dist = (targetPos.Position - mapPos.Position).Length();

                if (dist > npc.VisionRadius)
                {
                    continue;
                }
                if (dist >= closestDist)
                {
                    continue;
                }
                if (!CanSee(mapPos, targetPos, uid, target))
                {
                    continue;
                }

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
                if (result.HitEntity == targetEnt)
                {
                    return true;
                }
                if (result.HitEntity == sourceEnt)
                {
                    continue;
                }

                return false;
            }
            return true;
        }
    }
}
