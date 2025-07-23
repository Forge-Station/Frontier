using Robust.Shared.Random;

namespace Content.Server._Forge.BluespaceHarvester;

public sealed class BluespaceHarvesterRiftSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<BluespaceHarvesterRiftComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            comp.PassiveSpawnAccumulator += frameTime;
            if (comp.PassiveSpawnAccumulator >= comp.PassiveSpawnCooldown)
            {
                comp.PassiveSpawnAccumulator -= comp.PassiveSpawnCooldown;
                comp.PassiveSpawnAccumulator += _random.NextFloat(comp.PassiveSpawnCooldown / 2f);

                // Random, not particularly dangerous mob.
                Spawn(_random.Pick(comp.PassiveSpawn), xform.Coordinates);
            }

            comp.SpawnAccumulator += frameTime;

            if (comp.SpawnAccumulator < comp.SpawnCooldown)
                continue;

            comp.SpawnAccumulator -= comp.SpawnCooldown;
            comp.PassiveSpawnAccumulator += _random.NextFloat(comp.SpawnCooldown);

            UpdateSpawn((uid, comp, xform));
        }
    }

    private void UpdateSpawn(Entity<BluespaceHarvesterRiftComponent, TransformComponent> ent)
    {
        var rift = ent.Comp1;
        var xform = ent.Comp2;

        if (rift.SpawnedMobs >= rift.MaxTotalMobs)
            return;

        var count = 0;
        while (rift.Danger != 0 && count < 3 && rift.SpawnedMobs < rift.MaxTotalMobs)
        {
            count++;

            var pickable = new List<EntitySpawn>();
            foreach (var spawn in rift.Spawn)
            {
                if (spawn.Cost <= rift.Danger)
                    pickable.Add(spawn);
            }

            if (pickable.Count == 0)
            {
                rift.Danger = 0;
                break;
            }

            var pick = _random.Pick(pickable);

            rift.Danger -= pick.Cost;
            Spawn(pick.Id, xform.Coordinates);
            rift.SpawnedMobs++;
        }
    }
}
