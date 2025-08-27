using System.Linq;
using Content.Shared._ADT.Turrets.Components;
using Content.Shared._ADT.Turrets.Events;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Ghost;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;

namespace Content.Shared._ADT.Turrets.Systems;

public sealed class TurretControllerSystem : EntitySystem
{
    [Dependency] private readonly SharedMindSystem _mindSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TurretControllerComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<TurretControllerComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<TurretControllerComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<TurretControllerComponent, LinkAttemptEvent>(OnLinkAttempt);
        SubscribeLocalEvent<TurretControllerComponent, NewLinkEvent>(OnNewLink);
        SubscribeLocalEvent<TurretControllerComponent, ReturnToBodyTurretEvent>(OnReturn);
    }

    public void OnReturn(EntityUid uid, TurretControllerComponent component, ReturnToBodyTurretEvent args)
    {
        component.CurrentUser = null;
        component.CurrentTurret = null;
    }

    public void OnLinkAttempt(EntityUid uid, TurretControllerComponent component, LinkAttemptEvent args)
    {
        if (component.CurrentUser is not null)
            args.Cancel();
    }

    private void OnUseInHand(EntityUid uid, TurretControllerComponent comp, UseInHandEvent args)
    {
        if (args.Handled)
            return;

        TryStartControl(uid, comp, args.User);
        args.Handled = true;
    }

    private void OnActivateInWorld(EntityUid uid, TurretControllerComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        TryStartControl(uid, component, args.User);
        args.Handled = true;
    }

    private void TryStartControl(EntityUid controllerUid, TurretControllerComponent controllerComp, EntityUid user)
    {
        if (TryComp<GhostComponent>(user, out _) || TryComp<TurretControllableComponent>(user, out _))
            return;

        if (!TryComp<DeviceLinkSourceComponent>(controllerUid, out var linkSource))
            return;

        if (controllerComp.CurrentUser != null)
        {
            var msg = Loc.GetString("machine-already-in-use", ("machine", controllerUid));
            _popupSystem.PopupEntity(msg, controllerUid, user);
            return;
        }

        if (!TryComp<MindContainerComponent>(user, out var userMind) || !userMind.HasMind)
            return;

        if (linkSource.LinkedPorts.Count == 0)
            return;

        var target = linkSource.LinkedPorts.First().Key;
        if (!TryComp<TurretControllableComponent>(target, out _))
            return;

        if (TryComp<MindContainerComponent>(target, out var tMind) && tMind.HasMind)
        {
            var msg = Loc.GetString("machine-already-in-use", ("machine", target));
            _popupSystem.PopupEntity(msg, controllerUid, user);
            return;
        }

        controllerComp.CurrentUser = user;
        controllerComp.CurrentTurret = target;
        RaiseLocalEvent(target, new GettingControlledEvent(user, controllerUid));
        _mindSystem.ControlMob(user, target);
    }

    public void OnShutdown(EntityUid uid, TurretControllerComponent component, ComponentShutdown args)
    {
        if (component.CurrentUser is not null && component.CurrentTurret is not null)
            RaiseLocalEvent(component.CurrentTurret.Value, new ControlReturnActionEvent());
    }

    public void OnNewLink(EntityUid uid, TurretControllerComponent component, NewLinkEvent args)
    {
        if (TryComp<TurretControllableComponent>(args.Sink, out _))
            component.CurrentTurret = args.Sink;
    }
}
