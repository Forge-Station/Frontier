using Content.Server.Hands.Systems;
using Content.Server.Mind;
using Content.Shared._Forge.Skeleton;
using Content.Shared._NF.Bank;
using Content.Shared._NF.Bank.Components;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Hands.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Polymorph;
using Robust.Server.Containers;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._Forge.Skeleton;

[RegisterComponent]
public sealed partial class SkeletonReformOnDeathComponent : Component
{
    [DataField(required: true)]
    public ProtoId<PolymorphPrototype> PolymorphId;
}

public sealed class SkeletonReformOnDeathSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _acts = default!;
    [Dependency] private readonly SharedBankSystem _bank = default!;
    [Dependency] private readonly ContainerSystem _cont = default!;
    [Dependency] private readonly IEntityManager _ent = default!;
    [Dependency] private readonly HandsSystem _hands = default!;
    [Dependency] private readonly InventorySystem _inv = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _tim = default!;


    public override void Initialize()
    {
        SubscribeLocalEvent<SkeletonReformOnDeathComponent, MobStateChangedEvent>(OnDeath);
        SubscribeLocalEvent<SkeletonReformComponent, ComponentShutdown>(OnSkullGone);
    }

    private void OnDeath(EntityUid body, SkeletonReformOnDeathComponent comp, MobStateChangedEvent ev)
    {
        if (ev.NewMobState != MobState.Dead)
            return;

        if (!_proto.TryIndex(comp.PolymorphId, out var poly)
            || string.IsNullOrWhiteSpace(poly.Configuration.Entity))
            return;
        var skullProto = poly.Configuration.Entity;

        if (_ent.TryGetComponent(body, out HandsComponent? handsC))
        {
            foreach (var kvp in handsC.Hands)
            {
                _hands.TryDrop(body, kvp.Value, null, false, false, handsC);
            }
        }

        if (_ent.TryGetComponent(body, out InventoryComponent? invC))
        {
            foreach (var slot in invC.Slots)
            {
                var slotId = slot.Name;
                _inv.TryUnequip(body, slotId, false, true, true, invC);
            }
        }


        var coords = _ent.GetComponent<TransformComponent>(body).Coordinates;
        var skull = _ent.SpawnEntity(skullProto, coords);

        _ent.EnsureComponent<ContainerManagerComponent>(skull);
        var pocket = _cont.EnsureContainer<Container>(skull, "SkeletonBody");
        _cont.Insert(body, pocket);

        if (_mind.TryGetMind(body, out var mindUid, out _))
            _mind.TransferTo(mindUid, skull);
        if (_ent.TryGetComponent(skull, out SkeletonReformComponent? reform))
            reform.OriginalBody = body;
        if (_ent.TryGetComponent(body, out BankAccountComponent? bank))
            _bank.SetBalance(skull, bank.Balance);
        if (_ent.TryGetComponent(body, out MetaDataComponent? md))
            _meta.SetEntityName(skull, md.EntityName);
        if (!string.IsNullOrWhiteSpace(reform?.ActionPrototype))
        {
            _ent.EnsureComponent<ActionsComponent>(skull);
            _acts.AddAction(skull, ref reform.ActionEntity, reform.ActionPrototype);

            if (reform is { StartDelayed: true, ReformTime: > 0, ActionEntity: not null })
            {
                var now = _tim.CurTime;
                _acts.SetCooldown(reform.ActionEntity.Value, now, now + TimeSpan.FromSeconds(reform.ReformTime));
            }
        }
    }

    private void OnSkullGone(EntityUid skull, SkeletonReformComponent comp, ComponentShutdown _)
    {
        if (comp.OriginalBody is not { } body || !_ent.EntityExists(body))
            return;

        if (_cont.TryGetContainer(skull, "SkeletonBody", out var c) && c.Contains(body))
            QueueDel(body);
    }
}
