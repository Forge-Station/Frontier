using Content.Server.Spawners.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Mobs.Components; // Forge-Change
using Content.Shared.Mobs; // Forge-Change

namespace Content.Server.Spawners.EntitySystems;

public sealed class SpawnerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TimedSpawnerComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<TimedSpawnerComponent>();
        while (query.MoveNext(out var uid, out var timedSpawner))
        {
            if (timedSpawner.NextFire > curTime)
                continue;

            OnTimerFired(uid, timedSpawner);

            timedSpawner.NextFire += timedSpawner.IntervalSeconds;
        }
    }

    private void OnMapInit(Entity<TimedSpawnerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextFire = _timing.CurTime + ent.Comp.IntervalSeconds;
    }

    private void OnTimerFired(EntityUid uid, TimedSpawnerComponent component)
    {
        if (!_random.Prob(component.Chance))
            return;

        // Forge-Change start
        if (component.MaximumEntitiesPerGrid > 0)
        {
            CleanupSpawnedEntities(uid, component);

            var aliveEntitiesCount = CountAliveEntities(component);

            if (aliveEntitiesCount >= component.MaximumEntitiesPerGrid)
                return;

            var maxAllowedEntities = component.MaximumEntitiesPerGrid - aliveEntitiesCount;
            var maxToSpawn = Math.Min(component.MaximumEntitiesSpawned, maxAllowedEntities);

            if (maxToSpawn < component.MinimumEntitiesSpawned)
                return;

            var number = _random.Next(component.MinimumEntitiesSpawned, maxToSpawn);

            var coordinates = Transform(uid).Coordinates;

            for (var i = 0; i < number; i++)
            {
                var entity = _random.Pick(component.Prototypes);
                var spawned = SpawnAtPosition(entity, coordinates);
                component.SpawnedEntities.Add(spawned);
            }
        }
        else
        {
            var number = _random.Next(component.MinimumEntitiesSpawned, component.MaximumEntitiesSpawned);
            var coordinates = Transform(uid).Coordinates;

            for (var i = 0; i < number; i++)
            {
                var entity = _random.Pick(component.Prototypes);
                SpawnAtPosition(entity, coordinates);
            }
        }
        // Forge-Change end
    }

    // Forge-Change start
    private void CleanupSpawnedEntities(EntityUid spawnerUid, TimedSpawnerComponent component)
    {
        var toRemove = new List<EntityUid>();

        foreach (var entityUid in component.SpawnedEntities)
        {
            if (!Exists(entityUid))
            {
                toRemove.Add(entityUid);
            }
        }

        foreach (var entityUid in toRemove)
        {
            component.SpawnedEntities.Remove(entityUid);
        }
    }

    private int CountAliveEntities(TimedSpawnerComponent component)
    {
        if (component.MaximumEntitiesPerGrid <= 0)
            return 0;

        var count = 0;

        foreach (var entityUid in component.SpawnedEntities)
        {
            if (!Exists(entityUid))
                continue;

            if (TryComp<MobStateComponent>(entityUid, out var mobState))
            {
                if (mobState.CurrentState == MobState.Alive ||
                    mobState.CurrentState == MobState.Critical)
                {
                    count++;
                }
            }
            else
            {
                count++;
            }
        }

        return count;
    }
    // Forge-Change end
}
