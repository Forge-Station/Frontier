// SPDX-FileCopyrightText: 2025 bitcrushing
//
// SPDX-License-Identifier: MPL-2.0

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Weapons.Hitscan.Components;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._Mono.Hitscan;

public sealed class HitscanDiffractionTest : InteractionTest
{
    [Test]
    [TestOf(typeof(HitscanDiffractComponent))]
    [TestOf(typeof(HitscanDiffractTargetComponent))]
    public async Task TestBasicDiffraction()
    {
        // Spawn test entities
        await SpawnTarget("BaseStructure");
        await Server.WaitPost(() => SEntMan.AddComponent<HitscanDiffractTargetComponent>(STarget!.Value));

        EntityUid hitscan = default;
        await Server.WaitPost(() =>
        {
            hitscan = SEntMan.SpawnEntity("RedLaser", SEntMan.GetCoordinates(PlayerCoords));
            SEntMan.AddComponent<HitscanDiffractComponent>(hitscan);
        });

        await RunTicks(5);
        AssertExists(SEntMan.GetNetEntity(hitscan));
        Assert.That(STarget, Is.Not.Null);
        AssertExists(Target);

        // Count entities before diffraction
        var entitiesBefore = SEntMan.Count<HitscanDiffractComponent>();

        // Fire hitscan trace event
        var traceEvent = new HitscanTraceEvent
        {
            FromCoordinates = Transform.GetMoverCoordinates(hitscan),
            ShotDirection = Vector2.UnitX,
            Gun = EntityUid.Invalid,
            Shooter = SPlayer
        };

        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(hitscan, ref traceEvent));
        await RunTicks(5);

        // Original hitscan should be deleted
        AssertDeleted(SEntMan.GetNetEntity(hitscan));

        // Default defracted beam count is 5, assert that 5 have been spawned
        var entitiesAfter = SEntMan.Count<HitscanDiffractComponent>();
        Assert.That(entitiesAfter, Is.EqualTo(entitiesBefore + 4));

        Assert.That(STarget, Is.Not.Null);
        AssertExists(Target);
    }

    [Test]
    [TestOf(typeof(HitscanDiffractTargetComponent))]
    public async Task TestInactiveDiffractionTarget()
    {
        // Spawn target entity with Active = false datafield
        await SpawnTarget("BaseStructure");
        await Server.WaitPost(() =>
        {
            var comp = SEntMan.AddComponent<HitscanDiffractTargetComponent>(STarget!.Value);
            comp.Active = false;
        });

        EntityUid hitscan = default;
        await Server.WaitPost(() =>
        {
            hitscan = SEntMan.SpawnEntity("RedLaser", SEntMan.GetCoordinates(PlayerCoords));
            SEntMan.AddComponent<HitscanDiffractComponent>(hitscan);
        });

        await RunTicks(5);
        AssertExists(SEntMan.GetNetEntity(hitscan));
        Assert.That(STarget, Is.Not.Null);
        AssertExists(Target);

        // Fire hitscan trace event
        var traceEvent = new HitscanTraceEvent
        {
            FromCoordinates = Transform.GetMoverCoordinates(hitscan),
            ShotDirection = Vector2.UnitX,
            Gun = EntityUid.Invalid,
            Shooter = SPlayer
        };

        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(hitscan, ref traceEvent));
        await RunTicks(5);

        // Original hitscan should still exist
        AssertExists(SEntMan.GetNetEntity(hitscan));
        Assert.That(STarget, Is.Not.Null);
        AssertExists(Target);
    }

    [Test]
    [TestOf(typeof(HitscanDiffractComponent))]
    public async Task TestNonDiffractingHitscan()
    {
        // Spawn diffracting target, hitscan with no diffract component
        await SpawnTarget("BaseStructure");
        await Server.WaitPost(() => SEntMan.AddComponent<HitscanDiffractTargetComponent>(STarget!.Value));

        EntityUid hitscan = default;
        await Server.WaitPost(() =>
        {
            hitscan = SEntMan.SpawnEntity("RedLaser", SEntMan.GetCoordinates(PlayerCoords));
        });

        await RunTicks(5);
        AssertExists(SEntMan.GetNetEntity(hitscan));
        Assert.That(STarget, Is.Not.Null);
        AssertExists(Target);

        // Fire hitscan trace event
        var traceEvent = new HitscanTraceEvent
        {
            FromCoordinates = Transform.GetMoverCoordinates(hitscan),
            ShotDirection = Vector2.UnitX,
            Gun = EntityUid.Invalid,
            Shooter = SPlayer
        };

        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(hitscan, ref traceEvent));
        await RunTicks(5);

        // Original hitscan should still exist
        AssertExists(SEntMan.GetNetEntity(hitscan));
        Assert.That(STarget, Is.Not.Null);
        AssertExists(Target);
    }

    [Test]
    [TestOf(typeof(HitscanDiffractComponent))]
    public async Task TestBeamCountConfiguration()
    {
        // Spawn hitscan entity + diffract comp with custom beam count
        await SpawnTarget("BaseStructure");
        await Server.WaitPost(() => SEntMan.AddComponent<HitscanDiffractTargetComponent>(STarget!.Value));

        EntityUid hitscan = default;
        await Server.WaitPost(() =>
        {
            hitscan = SEntMan.SpawnEntity("RedLaser", SEntMan.GetCoordinates(PlayerCoords));
            var comp = SEntMan.AddComponent<HitscanDiffractComponent>(hitscan);
            comp.BeamCount = 3; // Custom beam count
        });

        await RunTicks(5);
        AssertExists(SEntMan.GetNetEntity(hitscan));

        // Count entities before diffraction
        var entitiesBefore = SEntMan.Count<HitscanDiffractComponent>();

        // Fire hitscan trace event
        var traceEvent = new HitscanTraceEvent
        {
            FromCoordinates = Transform.GetMoverCoordinates(hitscan),
            ShotDirection = Vector2.UnitX,
            Gun = EntityUid.Invalid,
            Shooter = SPlayer
        };

        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(hitscan, ref traceEvent));
        await RunTicks(5);

        // Assert 3 beams have been spawned
        var entitiesAfter = SEntMan.Count<HitscanDiffractComponent>();
        Assert.That(entitiesAfter, Is.EqualTo(entitiesBefore + 2));
    }

    [Test]
    [TestOf(typeof(HitscanDiffractComponent))]
    public async Task TestCustomDiffractedPrototype()
    {
        // Spawn hitscan entity with non-default diffracted beam proto
        await SpawnTarget("BaseStructure");
        await Server.WaitPost(() => SEntMan.AddComponent<HitscanDiffractTargetComponent>(STarget!.Value));

        EntityUid hitscan = default;
        await Server.WaitPost(() =>
        {
            hitscan = SEntMan.SpawnEntity("RedLaser", SEntMan.GetCoordinates(PlayerCoords));
            var comp = SEntMan.AddComponent<HitscanDiffractComponent>(hitscan);
            comp.DiffractedBeamPrototype = "SmallRedLaser";
            comp.BeamCount = 1; // Single beam
        });

        await RunTicks(5);
        AssertExists(SEntMan.GetNetEntity(hitscan));

        // Fire hitscan trace event
        var traceEvent = new HitscanTraceEvent
        {
            FromCoordinates = Transform.GetMoverCoordinates(hitscan),
            ShotDirection = Vector2.UnitX,
            Gun = EntityUid.Invalid,
            Shooter = SPlayer
        };

        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(hitscan, ref traceEvent));
        await RunTicks(5);

        // Original should be deleted
        AssertDeleted(SEntMan.GetNetEntity(hitscan));

        // Find the spawned SmallRedLaser entity
        var spawnedEntity = SEntMan.EntityQuery<MetaDataComponent>()
            .Where(ent => ent.EntityPrototype?.ID == "SmallRedLaser")
            .FirstOrDefault();

        Assert.That(spawnedEntity != null, Is.True, "SmallRedLaser should have been spawned");
    }

    [Test]
    [TestOf(typeof(HitscanDiffractComponent))]
    public async Task TestDiffractionSafeguard()
    {
        await SpawnTarget("BaseStructure");
        await Server.WaitPost(() => SEntMan.AddComponent<HitscanDiffractTargetComponent>(STarget!.Value));

        EntityUid hitscan = default;
        await Server.WaitPost(() =>
        {
            hitscan = SEntMan.SpawnEntity("RedLaser", SEntMan.GetCoordinates(PlayerCoords));
            var comp = SEntMan.AddComponent<HitscanDiffractComponent>(hitscan);
            comp.DiffractedBeamPrototype = null; // Default to RedLaser
            comp.BeamCount = 1;
        });

        await RunTicks(5);

        // Fire hitscan trace event
        var traceEvent = new HitscanTraceEvent
        {
            FromCoordinates = Transform.GetMoverCoordinates(hitscan),
            ShotDirection = Vector2.UnitX,
            Gun = EntityUid.Invalid,
            Shooter = SPlayer
        };

        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(hitscan, ref traceEvent));
        await RunTicks(5);

        // Ensure RedLaser is spawned
        var redLaserCount = SEntMan.EntityQuery<MetaDataComponent>()
            .Count(ent => ent.EntityPrototype?.ID == "RedLaser");

        Assert.That(redLaserCount, Is.GreaterThan(0), "RedLaser should have been spawned as safeguard");
    }
}
