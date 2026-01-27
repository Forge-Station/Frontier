using Content.Server.Administration.Logs;
using Content.Server.Popups;
using Content.Shared.Database;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mindshield.Components;
using Content.Shared._Forge.Overlord.Components;
using Robust.Shared.Containers;

namespace Content.Server._Forge.Overlord;

/// <summary>
/// System used for adding or removing components with a overlord implant
/// </summary>
public sealed class OverlordSystem : EntitySystem
{
    [Dependency] private readonly IAdminLogManager _adminLogManager = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OverlordImplantComponent, ImplantImplantedEvent>(OnImplantImplanted);
        SubscribeLocalEvent<OverlordImplantComponent, EntGotRemovedFromContainerMessage>(OnImplantDraw);
    }

    private void OnImplantImplanted(Entity<OverlordImplantComponent> ent, ref ImplantImplantedEvent ev)
    {
        var mob = ev.Implanted;
        if (mob == null)
            return;

        EnsureComp<OverlordComponent>(mob.Value);
        _adminLogManager.Add(LogType.Mind, LogImpact.Medium, $"{ToPrettyString(mob.Value)} was converted using OverlordImplant.");
        OverlordRemovalCheck(mob.Value, ev.Implant);
    }

    private void OverlordRemovalCheck(EntityUid implanted, EntityUid implant)
    {
        if (_container.TryGetContainer(implanted, ImplanterComponent.ImplantSlotId, out var implantContainer))
        {
            foreach (var ent in implantContainer.ContainedEntities)
            {
                if (HasComp<MindShieldImplantComponent>(ent))
                {
                    _popupSystem.PopupEntity(Loc.GetString("overlord-break-mindshield"), implanted);
                    PredictedQueueDel(ent);
                    break;
                }
            }
        }
    }

    private void OnImplantDraw(Entity<OverlordImplantComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        RemComp<OverlordComponent>(args.Container.Owner);
    }
}
