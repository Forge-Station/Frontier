using Content.Server.Mind;
using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared._Corvax;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Systems;
using Robust.Server.GameObjects;

namespace Content.Server._Corvax;

public sealed class SkeletonReformSystem : EntitySystem
{
    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly MindSystem _mindSystem = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<SkeletonReformComponent, ReformEvent>(OnReform);
    }

    private void OnReform(EntityUid skullUid, SkeletonReformComponent comp, ReformEvent args)
    {
        if (comp.OriginalBody is not { } bodyUid || !_entMan.EntityExists(bodyUid))
        {
            _popup.PopupEntity("Невозможно найти тело скелета.", skullUid, args.User);
            return;
        }

        if (!_entMan.TryGetComponent<MobStateComponent>(bodyUid, out _))
        {
            _popup.PopupEntity("Тело скелета недействительно.", skullUid, args.User);
            return;
        }

        if (_entMan.TryGetComponent<DamageableComponent>(bodyUid, out var damageable))
        {
            _damageable.SetAllDamage(bodyUid, damageable, FixedPoint2.Zero);
        }

        var mobStateSys = _entMan.System<MobStateSystem>();
        mobStateSys.ChangeMobState(bodyUid, MobState.Alive);

        if (_mindSystem.TryGetMind(bodyUid, out var mindId, out var mind))
        {
            _mindSystem.TransferTo(mindId, bodyUid, mind: mind);
        }

        _popup.PopupEntity("Скелет восстал из мёртвых!", bodyUid, args.User);
        QueueDel(skullUid);
    }

    public sealed class ReformEvent : EntityEventArgs
    {
        public EntityUid User;
        public ReformEvent(EntityUid user) => User = user;
    }
}
