using Content.Server.Shuttles.Components;
using Content.Shared.CCVar;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Robust.Shared.Timing; // Forge-Change

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    private void InitializeIFF()
    {
        SubscribeLocalEvent<IFFConsoleComponent, AnchorStateChangedEvent>(OnIFFConsoleAnchor);
        SubscribeLocalEvent<IFFConsoleComponent, IFFShowIFFMessage>(OnIFFShow);
        SubscribeLocalEvent<IFFConsoleComponent, IFFShowVesselMessage>(OnIFFShowVessel);
        SubscribeLocalEvent<GridSplitEvent>(OnGridSplit);
        SubscribeLocalEvent<ShuttleStealthComponent, ComponentStartup>(OnStealthStartup);
    }

    // Forge-Change-start
    private void UpdateIFF(float frameTime)
    {
        var query = EntityQueryEnumerator<ShuttleStealthComponent, IFFComponent>();
        var curTime = _gameTiming.CurTime;

        while (query.MoveNext(out var uid, out var stealth, out var iff))
        {
            if (stealth.HideEndTime.HasValue && stealth.HideEndTime < curTime)
            {
                // Stealth has expired, turn it off and start cooldown.
                RemoveIFFFlag(uid, IFFFlags.Hide, iff);
                stealth.HideEndTime = null;
                stealth.HideCooldownEndTime = curTime + TimeSpan.FromSeconds(stealth.StealthCooldown);
                Dirty(uid, stealth);
            }
        }
    }

    private void OnStealthStartup(EntityUid uid, ShuttleStealthComponent component, ComponentStartup args)
    {
        if (component.StealthDuration < 0)
            component.StealthDuration = _cfg.GetCVar(CCVars.StealthDuration);

        if (component.StealthCooldown < 0)
            component.StealthCooldown = _cfg.GetCVar(CCVars.StealthCooldown);
    }
    // Forge-Change-End

    private void OnGridSplit(ref GridSplitEvent ev)
    {
        var splitMass = _cfg.GetCVar(CCVars.HideSplitGridsUnder);

        if (splitMass < 0)
            return;

        foreach (var grid in ev.NewGrids)
        {
            if (!_physicsQuery.TryGetComponent(grid, out var physics) ||
                physics.Mass > splitMass)
            {
                continue;
            }

            AddIFFFlag(grid, IFFFlags.HideLabel);
        }
    }

    private void OnIFFShow(EntityUid uid, IFFConsoleComponent component, IFFShowIFFMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null ||
            (component.AllowedFlags & IFFFlags.HideLabel) == 0x0)
        {
            return;
        }

        if (!args.Show)
        {
            AddIFFFlag(xform.GridUid.Value, IFFFlags.HideLabel);
        }
        else
        {
            RemoveIFFFlag(xform.GridUid.Value, IFFFlags.HideLabel);
        }
    }

    private void OnIFFShowVessel(EntityUid uid, IFFConsoleComponent component, IFFShowVesselMessage args)
    {
        if (!TryComp(uid, out TransformComponent? xform) || xform.GridUid == null ||
            (component.AllowedFlags & IFFFlags.Hide) == 0x0)
        {
            return;
        }

        var gridUid = xform.GridUid.Value;
        var iff = EnsureComp<IFFComponent>(gridUid);
        var stealth = EnsureComp<ShuttleStealthComponent>(gridUid);
        var curTime = _gameTiming.CurTime;

        if (!args.Show) // This means "hide vessel", i.e., turn ON the Hide flag
        {
            // Already hidden, do nothing.
            if ((iff.Flags & IFFFlags.Hide) != 0)
            {
                return;
            }

            if (stealth.HideCooldownEndTime.HasValue && stealth.HideCooldownEndTime > curTime)
            {
                var remaining = (stealth.HideCooldownEndTime.Value - curTime).TotalSeconds;
                _popup.PopupEntity(Loc.GetString("shuttle-iff-cooldown", ("seconds", Math.Ceiling(remaining))), uid, args.Actor);
                return;
            }

            AddIFFFlag(gridUid, IFFFlags.Hide, iff);
            stealth.HideEndTime = curTime + TimeSpan.FromSeconds(stealth.StealthDuration);
            stealth.HideCooldownEndTime = null;
        }
        else // This means "show vessel", i.e., turn OFF the Hide flag
        {
            // Already visible, do nothing.
            if ((iff.Flags & IFFFlags.Hide) == 0)
            {
                return;
            }

            RemoveIFFFlag(gridUid, IFFFlags.Hide, iff);
            stealth.HideEndTime = null;
            stealth.HideCooldownEndTime = curTime + TimeSpan.FromSeconds(stealth.StealthCooldown);
        }

        Dirty(gridUid, stealth); // Forge-Change
    }

    private void OnIFFConsoleAnchor(EntityUid uid, IFFConsoleComponent component, ref AnchorStateChangedEvent args)
    {
        // If we anchor / re-anchor then make sure flags up to date.
        if (!args.Anchored ||
            !TryComp(uid, out TransformComponent? xform) ||
            !TryComp<IFFComponent>(xform.GridUid, out var iff))
        {
            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = component.AllowedFlags,
                Flags = IFFFlags.None,
                HideEndTime = null, // Forge-Change
                HideCooldownEndTime = null, // Forge-Change
            });
        }
        else
        {
            TryComp<ShuttleStealthComponent>(xform.GridUid, out var stealth); // Forge-Change

            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = component.AllowedFlags,
                Flags = iff.Flags,
                HideEndTime = stealth?.HideEndTime, // Forge-Change
                HideCooldownEndTime = stealth?.HideCooldownEndTime, // Forge-Change
            });
        }
    }

    protected override void UpdateIFFInterfaces(EntityUid gridUid, IFFComponent component)
    {
        base.UpdateIFFInterfaces(gridUid, component);
        TryComp<ShuttleStealthComponent>(gridUid, out var stealth);
        var query = AllEntityQuery<IFFConsoleComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.GridUid != gridUid)
                continue;

            _uiSystem.SetUiState(uid, IFFConsoleUiKey.Key, new IFFConsoleBoundUserInterfaceState()
            {
                AllowedFlags = comp.AllowedFlags,
                Flags = component.Flags,
                HideEndTime = stealth?.HideEndTime, // Forge-Change
                HideCooldownEndTime = stealth?.HideCooldownEndTime, // Forge-Change
            });
        }
    }
}
