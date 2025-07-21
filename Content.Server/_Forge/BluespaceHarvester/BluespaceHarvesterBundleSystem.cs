using Content.Shared.Destructible;
using Content.Shared.Storage.Components;
using Robust.Shared.Random;

namespace Content.Server._Forge.BluespaceHarvester;

public sealed class BluespaceHarvesterBundleSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<_Forge.BluespaceHarvester.BluespaceHarvesterBundleComponent, StorageBeforeOpenEvent>(OnOpen);
        SubscribeLocalEvent<_Forge.BluespaceHarvester.BluespaceHarvesterBundleComponent, DestructionEventArgs>(OnDestruction);
    }

    private void OnOpen(Entity<_Forge.BluespaceHarvester.BluespaceHarvesterBundleComponent> bundle, ref StorageBeforeOpenEvent args)
    {
        CreateLoot(bundle);
    }

    private void OnDestruction(Entity<_Forge.BluespaceHarvester.BluespaceHarvesterBundleComponent> bundle, ref DestructionEventArgs args)
    {
        CreateLoot(bundle);
    }

    private void CreateLoot(Entity<_Forge.BluespaceHarvester.BluespaceHarvesterBundleComponent> bundle)
    {
        if (bundle.Comp.Spawned)
            return;

        var content = _random.Pick(bundle.Comp.Contents);
        var position = Transform(bundle.Owner).Coordinates;

        for (var i = 0; i < content.Amount; i++)
        {
            Spawn(content.PrototypeId, position);
        }

        bundle.Comp.Spawned = true;
    }
}
