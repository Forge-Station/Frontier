using Content.Shared.Disease;
using Robust.Shared.Prototypes;

namespace Content.Server.Xenoarchaeology.XenoArtifacts.Effects.Components;

[RegisterComponent]
public sealed partial class XAEInfectiousDiseaseComponent : Component
{
    [DataField("diseases", required: true)]
    public List<ProtoId<DiseasePrototype>> Diseases = new();

    [DataField("infectionChance")]
    public float InfectionChance = 0.7f;
}

