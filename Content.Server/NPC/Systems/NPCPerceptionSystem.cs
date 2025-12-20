using Content.Server.Interaction;
using Content.Server.Npc.Components;
using Content.Server.NPC.HTN;
using Content.Shared.NPC;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Systems;
using Content.Shared.Physics;
using Robust.Shared.Physics.Systems;
using Content.Shared.NPC.Components;
using Robust.Shared.Map;
using Microsoft.Extensions.ObjectPool;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Robust.Shared.Physics;

namespace Content.Server.NPC.Systems;

/// <summary>
/// Handles sight + sounds for NPCs.
/// </summary>
public sealed partial class NPCPerceptionSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly InteractionSystem _interaction = default!;
    [Dependency] private readonly NpcFactionSystem _npcFaction = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private ObjectPool<HashSet<Entity<MobStateComponent>>> _mobHashSetPool =
        new DefaultObjectPool<HashSet<Entity<MobStateComponent>>>(new SetPolicy<Entity<MobStateComponent>>());

    private const int MaxUpdatesPerTick = 10;

    private EntityQuery<NpcFactionMemberComponent> _factionQuery;
    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();

        _factionQuery = GetEntityQuery<NpcFactionMemberComponent>();
        _xformQuery = GetEntityQuery<TransformComponent>();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateRecentlyInjected(frameTime);
        UpdateVision(frameTime);
    }

    private void UpdateVision(float frameTime)
    {
        var updatesThisTick = 0;

        var query = EntityQueryEnumerator<ActiveNPCComponent, NPCPerceptionComponent, HTNComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var npc, out var htn, out var transform))
        {
            npc.ScanAccumulator += frameTime;

            if (npc.ScanAccumulator < npc.ScanFrequency)
                continue;

            if (updatesThisTick >= MaxUpdatesPerTick)
                break;

            npc.ScanAccumulator = 0 - _random.NextFloat(0, 0.2f);
            updatesThisTick++;

            UpdateTarget(uid, npc, htn, transform);
        }
    }

    private void UpdateTarget(EntityUid uid, NPCPerceptionComponent npc, HTNComponent htn, TransformComponent transform)
    {
        var ourMapPos = _transform.GetMapCoordinates(uid, transform);

        if (htn.Blackboard.TryGetValue<EntityUid>(npc.TargetKey, out var currentTarget, EntityManager) &&
            Exists(currentTarget) &&
            !Terminating(currentTarget))
        {
            var targetXform = _xformQuery.GetComponent(currentTarget);
            var targetMapPos = _transform.GetMapCoordinates(currentTarget, targetXform);

            if (ourMapPos.MapId == targetMapPos.MapId)
            {
                var distance = (targetMapPos.Position - ourMapPos.Position).Length();

                if (distance <= npc.VisionRadius && CanSee(ourMapPos, targetMapPos, uid, currentTarget, (int)npc.VisionMask))
                    return;
            }
        }

        FindNewTarget(uid, npc, htn, ourMapPos);
    }

    private void FindNewTarget(EntityUid uid, NPCPerceptionComponent npc, HTNComponent htn, MapCoordinates ourMapPos)
    {
        EntityUid? bestTarget = null;
        var closestDist = float.MaxValue;

        var mobs = _mobHashSetPool.Get();
        _lookup.GetEntitiesInRange<MobStateComponent>(ourMapPos, npc.VisionRadius, mobs);
        _factionQuery.TryGetComponent(uid, out var ourFaction);

        foreach (var targetEntity in mobs)
        {
            var target = targetEntity.Owner;

            if (target == uid)
                continue;

            // Don't target friendlies.
            _factionQuery.TryGetComponent(target, out var targetFaction);
            if (_npcFaction.IsEntityFriendly((uid, ourFaction), (target, targetFaction)))
                continue;

            var targetXform = _xformQuery.GetComponent(target);
            var targetMapPos = _transform.GetMapCoordinates(target, targetXform);
            var dist = (targetMapPos.Position - ourMapPos.Position).Length();

            if (dist >= closestDist)
            {
                continue;
            }

            if (!CanSee(ourMapPos, targetMapPos, uid, target, (int)npc.VisionMask))
                continue;

            closestDist = dist;
            bestTarget = target;
        }

        if (bestTarget != null)
        {
            htn.Blackboard.SetValue(npc.TargetKey, bestTarget.Value);
        }
        else
        {
            htn.Blackboard.Remove<EntityUid>(npc.TargetKey);
        }

        _mobHashSetPool.Return(mobs);
    }

    private bool CanSee(MapCoordinates origin, MapCoordinates target, EntityUid sourceEnt, EntityUid targetEnt, int collisionMask)
    {
        if (origin.MapId != target.MapId)
            return false;

        var direction = target.Position - origin.Position;
        var distance = direction.Length();

        if (distance < 0.01f)
            return true;

        var ray = new CollisionRay(origin.Position, direction.Normalized(), collisionMask);
        var rayResults = _physics.IntersectRay(origin.MapId, ray, distance, returnOnFirstHit: true);

        foreach (var result in rayResults)
        {
            if (result.HitEntity == targetEnt)
                return true;

            if (result.HitEntity == sourceEnt)
                continue;

            return false;
        }

        return true;
    }
}
