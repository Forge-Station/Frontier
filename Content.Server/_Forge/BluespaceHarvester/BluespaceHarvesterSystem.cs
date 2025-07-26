using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Shared._Forge.BluespaceHarvester;
using Content.Shared.Audio;
using Content.Shared.Destructible;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._Forge.BluespaceHarvester;

public sealed class BluespaceHarvesterSystem : EntitySystem
{
    private const float UpdateTime = 1.0f;
    [Dependency] private readonly SharedAmbientSoundSystem _ambientSound = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private readonly List<BluespaceHarvesterTap> _taps =
    [
        new() { Level = 0, Visual = BluespaceHarvesterVisuals.Tap0 },
        new() { Level = 1, Visual = BluespaceHarvesterVisuals.Tap1 },
        new() { Level = 5, Visual = BluespaceHarvesterVisuals.Tap2 },
        new() { Level = 10, Visual = BluespaceHarvesterVisuals.Tap3 },
        new() { Level = 15, Visual = BluespaceHarvesterVisuals.Tap4 },
        new() { Level = 20, Visual = BluespaceHarvesterVisuals.Tap5 },
    ];

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;


    private float _updateTimer;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BluespaceHarvesterComponent, PowerConsumerReceivedChanged>(ReceivedChanged);
        SubscribeLocalEvent<BluespaceHarvesterComponent, BluespaceHarvesterTargetLevelMessage>(OnTargetLevel);
        SubscribeLocalEvent<BluespaceHarvesterComponent, BluespaceHarvesterBuyMessage>(OnBuy);
        SubscribeLocalEvent<BluespaceHarvesterComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<BluespaceHarvesterComponent, GotEmaggedEvent>(OnEmagged);
    }

    private void OnEmagged(EntityUid uid, BluespaceHarvesterComponent comp, ref GotEmaggedEvent args)
    {
        if (HasComp<EmaggedComponent>(uid))
            return;

        args.Handled = true;
    }

    private void ReceivedChanged(Entity<BluespaceHarvesterComponent> ent, ref PowerConsumerReceivedChanged args)
    {
        ent.Comp.ReceivedPower = args.ReceivedPower;
        ent.Comp.DrawRate = args.DrawRate;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _updateTimer += frameTime;
        if (_updateTimer < UpdateTime)
            return;
        _updateTimer -= UpdateTime;

        var query = EntityQueryEnumerator<
            BluespaceHarvesterComponent,
            PowerConsumerComponent>();

        while (query.MoveNext(out var uid, out var harvester, out var consumer))
        {
            var need = GetUsagePower(harvester.CurrentLevel);

            if (harvester.CurrentLevel > 0 && harvester.ReceivedPower < need)
            {
                Reset(uid, harvester);
                consumer.DrawRate = 0;
                continue;
            }

            if (harvester.CurrentLevel < harvester.TargetLevel)
                harvester.CurrentLevel++;
            else if (harvester.CurrentLevel > harvester.TargetLevel)
                harvester.CurrentLevel = harvester.TargetLevel;

            consumer.DrawRate =
                harvester.CurrentLevel > 0 ? GetUsagePower(harvester.CurrentLevel) : 0;

            var gen = GetPointGeneration(uid, harvester);
            harvester.Points += gen;
            harvester.TotalPoints += gen;

            harvester.Danger += GetDangerPointGeneration(uid, harvester);
            if (harvester.Danger < 0)
                harvester.Danger = 0;

            if (harvester.Danger > harvester.DangerLimit &&
                _random.NextFloat() <= GetRiftChance(uid, harvester))
                SpawnRifts(uid, harvester);

            if (TryComp<AmbientSoundComponent>(uid, out var ambient))
                _ambientSound.SetAmbience(uid, harvester.Reset, ambient);

            UpdateAppearance(uid, harvester);
            UpdateUI(uid, harvester);
        }
    }


    private void OnDestruction(Entity<BluespaceHarvesterComponent> harvester, ref DestructionEventArgs args)
    {
        SpawnRifts(harvester.Owner, harvester.Comp);
    }

    private void OnTargetLevel(Entity<BluespaceHarvesterComponent> harvester,
        ref BluespaceHarvesterTargetLevelMessage args)
    {
        if (!harvester.Comp.Reset)
            harvester.Comp.Reset = true;

        harvester.Comp.TargetLevel = args.TargetLevel;
        UpdateUI(harvester.Owner, harvester.Comp);
    }

    private void OnBuy(Entity<BluespaceHarvesterComponent> harvester, ref BluespaceHarvesterBuyMessage args)
    {
        if (!harvester.Comp.Reset)
            return;

        if (!TryGetCategory(harvester.Owner, args.Category, out var info, harvester.Comp))
            return;

        var category = (BluespaceHarvesterCategoryInfo)info;

        if (harvester.Comp.Points < category.Cost)
            return;

        harvester.Comp.Points -= category.Cost; // Damn capitalism.
        SpawnLoot(harvester.Owner, category.PrototypeId, harvester.Comp);
    }

    private void UpdateAppearance(EntityUid uid, BluespaceHarvesterComponent? harvester = null)
    {
        if (!Resolve(uid, ref harvester))
            return;

        var level = harvester.CurrentLevel;
        BluespaceHarvesterTap? max = null;

        foreach (var tap in _taps)
        {
            if (tap.Level > level)
                continue;

            if (max == null || tap.Level > max.Level)
                max = tap;
        }

        // We get the biggest Tap of all, and replace it with a harvester.
        if (max == null)
            return;

        if (Emagged(uid))
            _appearance.SetData(uid, BluespaceHarvesterVisualLayers.Base, (int)harvester.RedspaceTap);
        else
            _appearance.SetData(uid, BluespaceHarvesterVisualLayers.Base, (int)max.Visual);

        _appearance.SetData(uid, BluespaceHarvesterVisualLayers.Effects, level != 0);
    }

    private void UpdateUI(EntityUid uid, BluespaceHarvesterComponent? harvester = null)
    {
        if (!Resolve(uid, ref harvester))
            return;

        _ui.SetUiState(uid,
            BluespaceHarvesterUiKey.Key,
            new BluespaceHarvesterBoundUserInterfaceState(
                harvester.TargetLevel,
                harvester.CurrentLevel,
                harvester.MaxLevel,
                GetUsagePower(harvester.CurrentLevel),
                GetUsageNextPower(harvester.CurrentLevel),
                harvester.Points,
                harvester.TotalPoints,
                GetPointGeneration(uid, harvester),
                harvester.Categories
            ));
    }

    private uint GetUsageNextPower(int level)
    {
        return GetUsagePower(level + 1);
    }

    private uint GetUsagePower(int level)
    {
        return (uint)(15_000 * Math.Pow(1.31, level));
    }


    /// <summary>
    /// Finds a free point in space and creates a prototype there, similar to a bluespace anomaly.
    /// </summary>
    private EntityUid? SpawnLoot(EntityUid uid, string prototype, BluespaceHarvesterComponent? harvester = null)
    {
        if (!Resolve(uid, ref harvester))
            return null;

        var xform = Transform(uid);
        var coords = xform.Coordinates;

        var validTiles = new List<EntityCoordinates>();
        var maxRadius = harvester.SpawnRadius > 0 ? (int)Math.Ceiling(harvester.SpawnRadius) : 8;

        for (var dx = -maxRadius; dx <= maxRadius; dx++)
        for (var dy = -maxRadius; dy <= maxRadius; dy++)
        {
            var offset = new Vector2(dx, dy);
            if (offset.Length() > harvester.SpawnRadius)
                continue;

            var testCoords = coords.Offset(offset);
            if (testCoords == coords)
                continue;
            var tile = testCoords.GetTileRef(EntityManager);
            if (tile == null)
                continue;

            var tileDef = tile.Value.GetContentTileDefinition();
            if (tileDef.ID == "Space")
                continue;

            if (_lookup.GetEntitiesIntersecting(testCoords.ToMap(EntityManager, _transform), LookupFlags.Static).Any())
                continue;

            validTiles.Add(testCoords);
        }

        if (validTiles.Count == 0)
            return null;

        var chosenCoords = validTiles[_random.Next(validTiles.Count)];

        _audio.PlayPvs(harvester.SpawnSound, uid);
        Spawn(harvester.SpawnEffect, chosenCoords);

        return Spawn(prototype, chosenCoords);
    }


    private int GetPointGeneration(EntityUid uid, BluespaceHarvesterComponent? harvester = null)
    {
        if (!Resolve(uid, ref harvester))
            return 0;

        var stable = GetStableLevel(uid, harvester);
        var level = harvester.CurrentLevel;

        int pointsPerLevel;

        if (level >= harvester.TriplePointMinLevel && level <= harvester.TriplePointMaxLevel)
            pointsPerLevel = 3;
        else
            pointsPerLevel = level <= stable ? 2 : 4;

        return level * pointsPerLevel * (Emagged(uid) ? 2 : 1) *
               (harvester.ResetTime == TimeSpan.Zero ? 1 : 0);
    }



    private int GetDangerPointGeneration(EntityUid uid, BluespaceHarvesterComponent? harvester = null)
    {
        if (!Resolve(uid, ref harvester))
            return 0;

        var stable = GetStableLevel(uid, harvester);

        if (harvester.CurrentLevel <= stable)
            return 0;

        return (harvester.CurrentLevel - stable) * 4;
    }


    private float GetRiftChance(EntityUid uid, BluespaceHarvesterComponent? harvester = null)
    {
        if (!Resolve(uid, ref harvester))
            return 0;

        return Emagged(uid) ? harvester.EmaggedRiftChance : harvester.RiftChance;
    }

    private int GetStableLevel(EntityUid uid, BluespaceHarvesterComponent? harvester = null)
    {
        if (!Resolve(uid, ref harvester))
            return 0;

        return Emagged(uid) ? harvester.EmaggedStableLevel : harvester.StableLevel;
    }

    private bool TryGetCategory(EntityUid uid,
        BluespaceHarvesterCategory target,
        [NotNullWhen(true)] out BluespaceHarvesterCategoryInfo? info,
        BluespaceHarvesterComponent? harvester = null)
    {
        info = null;
        if (!Resolve(uid, ref harvester))
            return false;

        foreach (var category in harvester.Categories)
        {
            if (category.Type != target)
                continue;

            info = category;
            return true;
        }

        return false;
    }

    private void Reset(EntityUid uid, BluespaceHarvesterComponent? harvester = null)
    {
        if (!Resolve(uid, ref harvester))
            return;
        harvester.Danger += harvester.DangerFromReset;
        harvester.Reset = false;
        harvester.TargetLevel = 0;
        harvester.CurrentLevel = 0;
        if (TryComp<PowerConsumerComponent>(uid, out var consumer))
            consumer.DrawRate = 0;
    }

    private bool Emagged(EntityUid uid)
    {
        return HasComp<EmaggedComponent>(uid);
    }

    private void SpawnRifts(EntityUid uid, BluespaceHarvesterComponent? harvester = null, int? danger = null)
    {
        if (!Resolve(uid, ref harvester) || harvester.RiftCount <= 0)
            return;

        var currentDanger = danger ?? harvester.Danger;

        var count = _random.Next(1, harvester.RiftCount + 1);

        var dangerPerRift = currentDanger / count;
        var remainder = currentDanger % count;

        for (var i = 0; i < count; i++)
        {
            var entity = SpawnLoot(uid, harvester.Rift, harvester);
            if (entity == null)
                continue;

            var comp = EnsureComp<BluespaceHarvesterRiftComponent>((EntityUid)entity);
            comp.Danger = dangerPerRift + (i < remainder ? 1 : 0);
        }

        harvester.Danger = 0;
    }
}
