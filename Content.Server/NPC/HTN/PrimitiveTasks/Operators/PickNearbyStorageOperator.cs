using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Content.Server.Access.Systems;
using Content.Server.NPC.Pathfinding;
using Content.Server.Storage.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Interaction;
using Content.Shared.NPC;
using Content.Shared.Lock;
using Content.Shared.Prying.Systems;
using Content.Shared.Tools.Systems;
using Robust.Shared.Map;
using Robust.Server.Containers;
using Content.Shared.Nutrition.EntitySystems;

namespace Content.Server.NPC.HTN.PrimitiveTasks.Operators;

/// <summary>
/// Picks a nearby closed storage container that is accessible.
/// </summary>
public sealed partial class PickNearbyStorageOperator : HTNOperator
{
    [Dependency] private readonly IEntityManager _entManager = default!;
    private PathfindingSystem _pathfinding = default!;
    private EntityLookupSystem _lookup = default!;
    private AccessReaderSystem _access = default!;
    private ContainerSystem _container = default!;
    private LockSystem _lock = default!;
    private OpenableSystem _openable = default!;
    private PryingSystem _prying = default!;
    private WeldableSystem _weldable = default!;
    private SharedTransformSystem _transform = default!;

    [DataField("range")]
    public float Range = 5f;

    [DataField("targetKey")]
    public string TargetKey = "Target";

    [DataField("targetCoordinatesKey")]
    public string TargetCoordinatesKey = "TargetCoordinates";

    /// <summary>
    /// Where the pathfinding result will be stored (if applicable). This gets removed after execution.
    /// </summary>
    [DataField("pathfindKey")]
    public string PathfindKey = NPCBlackboard.PathfindKey;

    public override void Initialize(IEntitySystemManager sysManager)
    {
        base.Initialize(sysManager);
        _lookup = sysManager.GetEntitySystem<EntityLookupSystem>();
        _pathfinding = sysManager.GetEntitySystem<PathfindingSystem>();
        _access = sysManager.GetEntitySystem<AccessReaderSystem>();
        _container = sysManager.GetEntitySystem<ContainerSystem>();
        _lock = sysManager.GetEntitySystem<LockSystem>();
        _openable = sysManager.GetEntitySystem<OpenableSystem>();
        _prying = sysManager.GetEntitySystem<PryingSystem>();
        _weldable = sysManager.GetEntitySystem<WeldableSystem>();
        _transform = sysManager.GetEntitySystem<SharedTransformSystem>();
    }

    public override async Task<(bool Valid, Dictionary<string, object>? Effects)> Plan(NPCBlackboard blackboard,
        CancellationToken cancelToken)
    {
        var owner = blackboard.GetValue<EntityUid>(NPCBlackboard.Owner);

        if (!blackboard.TryGetValue<EntityCoordinates>(NPCBlackboard.OwnerCoordinates, out var coordinates, _entManager))
        {
            return (false, null);
        }

        var storageQuery = _entManager.GetEntityQuery<EntityStorageComponent>();

        // Are we in a container?
        if (_container.TryGetContainingContainer(owner, out var container) && storageQuery.HasComponent(container.Owner))
        {
            var containerEnt = container.Owner;

            if (_openable.IsClosed(containerEnt) && !_lock.IsLocked(containerEnt) && !_weldable.IsWelded(containerEnt))
            {
                var xform = _entManager.GetComponent<TransformComponent>(containerEnt);
                return (true, new Dictionary<string, object>()
                {
                    { TargetKey, containerEnt },
                    { TargetCoordinatesKey, xform.Coordinates },
                });
            }
        }

        var targets = new List<(EntityUid, float)>();
        var ownerMap = _transform.ToMapCoordinates(coordinates);
        var flags = _pathfinding.GetFlags(blackboard);

        foreach (var entity in _lookup.GetEntitiesInRange(coordinates, Range))
        {
            if (entity == owner || !storageQuery.HasComponent(entity))
                continue;

            // Is it open?
            if (!_openable.IsClosed(entity))
                continue;

            // Is it locked?
            if (_lock.IsLocked(entity) && (flags & PathFlags.Prying) == 0)
                continue;

            // Is it welded shut?
            if (_weldable.IsWelded(entity))
                continue;

            // Do we have access?
            if (_entManager.HasComponent<AccessReaderComponent>(entity) && !_access.IsAllowed(owner, entity))
                continue;

            var targetMap = _transform.GetMapCoordinates(entity);

            if (ownerMap.MapId != targetMap.MapId)
                continue;

            var distance = (ownerMap.Position - targetMap.Position).LengthSquared();
            targets.Add((entity, distance));
        }

        if (targets.Count == 0)
        {
            return (false, null);
        }

        // Sort by distance
        targets.Sort((a, b) => a.Item2.CompareTo(b.Item2));

        if (targets.Count == 0)
        {
            return (false, null);
        }

        foreach (var (target, _) in targets)
        {
            var path = await _pathfinding.GetPath(
                owner,
                target,
                1f,
                cancelToken,
                flags: flags);

            if (path.Result != PathResult.Path)
                continue;

            var xform = _entManager.GetComponent<TransformComponent>(target);

            return (true, new Dictionary<string, object>() { { TargetKey, target }, { TargetCoordinatesKey, xform.Coordinates }, { PathfindKey, path } });
        }

        return (false, null);
    }
}
