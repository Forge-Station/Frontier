using System.Linq;
using System.Diagnostics.CodeAnalysis;
using Content.Shared._Forge.Contractor;
using Content.Shared._Forge.Contractor.Components;
using Content.Shared.Interaction.Events;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics.Events;
using Robust.Shared.Utility;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Random;
using System.Numerics;
using Content.Server.Buckle.Systems;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Chat;
using Robust.Shared.Player;
using Content.Server.Mind;
using Content.Server.Chat.Managers;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Components;
using Content.Shared.FixedPoint;
using Content.Shared._Forge.Contracts;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Forge.Contracts;

public sealed class ContractorEvacuationSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly BuckleSystem _buckle = default!;
    [Dependency] private readonly IChatManager _chat = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ISharedPlayerManager _player = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainer = default!;

    [ValidatePrototypeId<EntityPrototype>]
    private const string PortalPrototype = "ContractorEvacuationPortal";
    [ValidatePrototypeId<ReagentPrototype>]
    private const string Drug = "THC";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FlareEvacuationComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<EvacuationPortalComponent, StartCollideEvent>(TryTargetEvacuation);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<FlareEvacuationComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (_gameTiming.CurTime >= comp.UpdateTime && comp.Activate)
            {
                TryOpenPortal(uid, comp);
            }
        }
    }

    /// <summary>
    /// Handles using an evacuation flare in hand, schedules portal activation if contract conditions are met.
    /// </summary>
    private void OnUseInHand(Entity<FlareEvacuationComponent> ent, ref UseInHandEvent args)
    {
        if (!TryComp<ContractorComponent>(args.User, out var contractor) || !TryComp<ContractsComponent>(contractor.Uplink, out var contracts))
            return;

        if (!TryComp(args.User, out TransformComponent? xform) || xform.GridUid == null)
            return;

        foreach (var contract in contracts.ActiveContracts)
        {
            if (GetEntity(contract.EvacPoint) == xform.GridUid &&
                contract.Status == ContractStatus.Active)
            {
                ent.Comp.UpdateTime = _gameTiming.CurTime + TimeSpan.FromSeconds(20);
                ent.Comp.LinkedContractId = contract.ContractId;
                ent.Comp.Activate = true;
                return;
            }
        }
    }

    /// <summary>
    /// Attempts to open an evacuation portal at the flare's location if contract conditions are met.
    /// </summary>
    private void TryOpenPortal(EntityUid flare, FlareEvacuationComponent component)
    {
        if (!TryComp(flare, out TransformComponent? xform) || xform.GridUid == null
            || component.LinkedContractId == null)
            return;

        if (!TryFindContract(component.LinkedContractId, out var contract) ||
            contract.Status != ContractStatus.Active || xform.GridUid != GetEntity(contract.EvacPoint))
            return;

        var portal = Spawn(PortalPrototype, xform.Coordinates);
        EnsureComp<EvacuationPortalComponent>(portal).LinkedContractId = component.LinkedContractId;

        var sound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");
        _audio.PlayPvs(sound, portal);

        Del(flare);
    }

    /// <summary>
    /// Handles collision with an evacuation portal, evacuates the contract target if conditions are met.
    /// </summary>
    private void TryTargetEvacuation(Entity<EvacuationPortalComponent> ent, ref StartCollideEvent args)
    {
        var subject = args.OtherEntity;
        if (Transform(subject).Anchored || string.IsNullOrEmpty(ent.Comp.LinkedContractId))
            return;

        if (!TryFindContract(ent.Comp.LinkedContractId, out var contract) ||
            contract.Status != ContractStatus.Active)
            return;

        if (subject != GetEntity(contract.TargetEntity))
            return;

        HandleTargetEvacuation(subject);

        var ev = new ContractCompletedEvent(contract);
        RaiseLocalEvent(ref ev);

        var sound = new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg");
        _audio.PlayPvs(sound, ent);

        Del(ent);
    }

    /// <summary>
    /// Searches for an active contract by its ID.
    /// </summary>
    private bool TryFindContract(string contractId, [NotNullWhen(true)] out ActiveContract? contract)
    {
        var query = EntityQueryEnumerator<ContractsComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            contract = comp.ActiveContracts.FirstOrDefault(c => c.ContractId == contractId);
            if (contract != null)
                return true;
        }

        contract = null;
        return false;
    }

    /// <summary>
    /// Handles the evacuation process for the contract target, including moving them to a safe location and applying effects.
    /// </summary>
    private void HandleTargetEvacuation(EntityUid target)
    {
        _buckle.TryUnbuckle(target, target, false);
        if (TryComp<PullableComponent>(target, out var pullable) && !_pulling.TryStopPull(target, pullable))
            return;

        var targetXform = Transform(target);
        var currentMap = _transform.GetMapCoordinates(targetXform);

        var angle = _random.NextFloat(0, MathF.PI * 2);
        var distance = _random.Next(10000, 20000);
        var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;
        var desiredPosition = currentMap.Position + offset;

        var path = new ResPath("/Maps/Forge/Shuttles/Scrap/oldpallet.yml");
        if (!_mapLoader.TryLoadGrid(targetXform.MapID, path, out var gridUid, offset: desiredPosition))
            return;

        var gridCoords = Transform(gridUid.Value).Coordinates;
        var safeCoords = FindSafePositionOnGrid(gridUid.Value, gridCoords);

        _transform.SetCoordinates(target, safeCoords);
        if (_mind.TryGetMind(target, out _, out var mind) && mind is { UserId: not null }
            && _player.TryGetSessionById(mind.UserId, out var session))
        {
            var message = Loc.GetString("target-evacuation-greeting");
            var wrappedMessage = Loc.GetString("chat-manager-server-wrap-message", ("message", message));
            _chat.ChatMessageToOne(ChatChannel.Server, message, wrappedMessage, default, false, session.Channel,
                Color.OrangeRed);
        }

        // A joke about a dill cupcake would be appropriate here.
        if (_solutionContainer.TryGetInjectableSolution(target, out var targetSolution, out _))
        {
            var solution = new Solution(Drug, FixedPoint2.New(50));
            _solutionContainer.TryTransferSolution(targetSolution.Value, solution, solution.Volume);
        }
    }

    /// <summary>
    /// Finds a safe position on the specified grid for evacuation.
    /// </summary>
    private EntityCoordinates FindSafePositionOnGrid(EntityUid gridUid, EntityCoordinates gridCoords)
    {
        if (!TryComp<MapGridComponent>(gridUid, out var grid))
            return gridCoords;

        var aabb = grid.LocalAABB;
        var center = aabb.Center;
        var halfSize = aabb.Size / 2f;
        var radius = Math.Min(halfSize.X, halfSize.Y) * 0.8f;

        var centerCoords = new EntityCoordinates(gridUid, center);
        if (IsPositionSafe(centerCoords))
            return centerCoords;

        var spiralSteps = 8;
        var stepSize = 1.0f;

        for (int step = 1; step * stepSize <= radius; step++)
        {
            for (int i = 0; i < spiralSteps; i++)
            {
                var angle = (float)i / spiralSteps * MathHelper.TwoPi;
                var offset = new Vector2(
                    (float)Math.Cos(angle) * stepSize * step,
                    (float)Math.Sin(angle) * stepSize * step);

                var testPos = center + offset;
                var testCoords = new EntityCoordinates(gridUid, testPos);

                if (IsPositionSafe(testCoords))
                    return testCoords;
            }
        }

        return new EntityCoordinates(gridUid, center + new Vector2(0.5f, 0.5f));
    }

    /// <summary>
    /// Checks if the specified coordinates are safe for evacuation (not empty and not occupied).
    /// </summary>
    private bool IsPositionSafe(EntityCoordinates coords)
    {
        var gridUid = _transform.GetGrid(coords.EntityId);
        if (gridUid != null && TryComp<MapGridComponent>(gridUid.Value, out var gridComp))
        {
            var tile = gridComp.GetTileRef(coords);
            if (tile.Tile.IsEmpty)
                return false;
        }

        var mapCoords = _transform.ToMapCoordinates(coords);
        return !_lookup.GetEntitiesIntersecting(mapCoords, LookupFlags.Static | LookupFlags.Dynamic).Any();
    }
}
