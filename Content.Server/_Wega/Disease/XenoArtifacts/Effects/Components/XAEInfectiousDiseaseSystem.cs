using Content.Server.Disease;
using Content.Shared.Disease.Components;
using Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;
using Content.Shared.Xenoarchaeology.Artifact;
using Robust.Shared.Random;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Systems;

public sealed class XAEInfectiousDiseaseSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DiseaseSystem _diseaseSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<XAEInfectiousDiseaseComponent, XenoArtifactActivatedEvent>(OnActivate);
    }

    private void OnActivate(EntityUid uid, XAEInfectiousDiseaseComponent component, ref XenoArtifactActivatedEvent args)
    {
        if (args.User is not { } user || !TryComp<DiseaseCarrierComponent>(user, out var carrier))
            return;

        if (component.Diseases.Count == 0)
            return;

        var diseaseId = _random.Pick(component.Diseases);
        _diseaseSystem.TryInfect(carrier, diseaseId, component.InfectionChance, forced: true);
    }
}

