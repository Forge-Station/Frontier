using Content.Server.Mind;
using Content.Server.Polymorph.Systems;
using Content.Shared.Mobs;
using Content.Shared.Polymorph;
using Robust.Shared.Prototypes;

namespace Content.Server._Corvax;

[RegisterComponent]
public sealed partial class SkeletonReformOnDeathComponent : Component
{
    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> PolymorphId;
}

public sealed class SkeletonReformOnDeathSystem : EntitySystem
{
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SkeletonReformOnDeathComponent, MobStateChangedEvent>(OnMobStateChanged);
    }

    private void OnMobStateChanged(EntityUid uid, SkeletonReformOnDeathComponent component, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        if (!_mindSystem.TryGetMind(uid, out var mindId, out var mind))
            return;

        if (mind.Session == null)
            return;

        var newEntity = _polymorph.PolymorphEntity(uid, component.PolymorphId);
        if (newEntity == null)
            return;

        _mindSystem.TransferTo(mindId, newEntity.Value, mind: mind);
        QueueDel(uid);
    }
}
