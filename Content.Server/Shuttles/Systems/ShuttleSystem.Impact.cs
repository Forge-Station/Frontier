using System.Numerics;
using Content.Server.Shuttles.Components;
using Robust.Server.GameObjects;
using Content.Shared.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Map.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs.Components;
using Content.Shared.Buckle.Components;
using Content.Server._NF.Shuttles.Components;
using Content.Shared.Tiles;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Explosion.Components;
using Robust.Shared.Physics.Components; // Добавляем пространство имен для ExplosionSystem

namespace Content.Server.Shuttles.Systems;

public sealed partial class ShuttleSystem
{
    [Dependency] private readonly MapSystem _mapSys = default!;
    [Dependency] private readonly DamageableSystem _damageSys = default!;
    [Dependency] private readonly ExplosionSystem _explosionSystem = default!; // Добавляем ExplosionSystem

    private readonly SoundCollectionSpecifier _shuttleImpactSound = new("ShuttleImpactSound");

    private void InitializeImpact()
    {
        SubscribeLocalEvent<ShuttleComponent, StartCollideEvent>(OnShuttleCollide);
    }

    private void OnShuttleCollide(EntityUid uid, ShuttleComponent component, ref StartCollideEvent args)
    {
        if (TryComp<ProtectedGridComponent>(uid, out var ourProtected) && ourProtected.NoGridCollision ||
            TryComp<ProtectedGridComponent>(args.OtherEntity, out var otherProtected) && otherProtected.NoGridCollision)
        {
            return;
        }

        if (!TryComp<MapGridComponent>(uid, out var ourGrid) ||
            !TryComp<MapGridComponent>(args.OtherEntity, out var otherGrid) ||
            !TryComp<ShuttleImpactComponent>(uid, out var impact))
            return;

        var ourBody = args.OurBody;
        var otherBody = args.OtherBody;

        var ourXform = Transform(uid);

        if (ourXform.MapUid == null)
            return;

        var otherXform = Transform(args.OtherEntity);

        var ourPoint = Vector2.Transform(args.WorldPoint, _transform.GetInvWorldMatrix(ourXform));
        var otherPoint = Vector2.Transform(args.WorldPoint, _transform.GetInvWorldMatrix(otherXform));

        var ourVelocity = _physics.GetLinearVelocity(uid, ourPoint, ourBody, ourXform);
        var otherVelocity = _physics.GetLinearVelocity(args.OtherEntity, otherPoint, otherBody, otherXform);
        var jungleDiff = (ourVelocity - otherVelocity).Length();

        if (jungleDiff < impact.MinimumImpactVelocity)
        {
            return;
        }

        var energy = ourBody.Mass * Math.Pow(jungleDiff, 2) / 2;
        var dir = (ourVelocity.Length() > otherVelocity.Length() ? ourVelocity : -otherVelocity).Normalized();

        // Получаем список тайлов для обработки
        var ourTiles = GetImpactedTiles(ourPoint, dir, impact.TileBreakLength, impact.TileBreakWidth);
        var otherTiles = GetImpactedTiles(otherPoint, dir, impact.TileBreakLength, impact.TileBreakWidth);

        // Обрабатываем каждый тайл
        foreach (var tile in ourTiles)
        {
            ProcessTile(uid, ourGrid, tile, (float)energy, -dir, impact);
        }
        foreach (var tile in otherTiles)
        {
            ProcessTile(args.OtherEntity, otherGrid, tile, (float)energy, dir, impact);
        }

        var coordinates = new EntityCoordinates(ourXform.MapUid.Value, args.WorldPoint);
        var volume = MathF.Min(10f, 1f * MathF.Pow(jungleDiff, 0.5f) - 5f);
        var audioParams = AudioParams.Default.WithVariation(SharedContentAudioSystem.DefaultVariation).WithVolume(volume);

        _audio.PlayPvs(_shuttleImpactSound, coordinates, audioParams);
    }

    private void ProcessTile(EntityUid uid, MapGridComponent grid, Vector2i tile, float energy, Vector2 dir, ShuttleImpactComponent impact)
    {
        var mobQuery = GetEntityQuery<MobStateComponent>();
        var explosiveQuery = GetEntityQuery<ExplosiveComponent>(); // Получаем запрос на ExplosiveComponent
        var physicsQuery = GetEntityQuery<PhysicsComponent>();

        // Рассчитываем модификатор урона на основе угла столкновения
        var damageMultiplier = CalculateDamageMultiplier(dir);

        DamageSpecifier mobDamage = new();
        var damageMob = energy * impact.MobDamageMultiplier * damageMultiplier;
        damageMob = Math.Min(damageMob, impact.MaxMobDamage);
        mobDamage.DamageDict = new()
        {
            { "Blunt", (float)(damageMob * 0.5f) },
            { "Piercing", (float)(damageMob * 0.3f) },
            { "Heat", (float)(damageMob * 0.2f) }
        };
        // Рассчитываем модификатор отбрасывания на основе угла столкновения
        var throwMultiplier = CalculateThrowMultiplier(dir);

        if (energy >= impact.TileBreakEnergy)
            StunMobsInTile(uid, grid, impact);

        var tileBreakEnergy = impact.TileBreakEnergy;
        var tileBreakLength = impact.TileBreakLength;
        var tileBreakWidth = impact.TileBreakWidth;

        if (energy > tileBreakEnergy)
        {
            for (var i = 0; i < tileBreakLength; i++)
            {
                for (var j = -tileBreakWidth; j <= tileBreakWidth; j++)
                {
                    var offsetTile = tile + new Vector2i(j, i);
                    // Используем dir для определения направления разрушения
                    var currentDir = dir;
                    if (currentDir.X != 0)
                    {
                        offsetTile = tile + new Vector2i(i, j);
                    }

                    if (energy > tileBreakEnergy * (1 - (MathF.Abs(j) / (tileBreakWidth + 1)) * 0.5f))
                    {
                        foreach (EntityUid localUid in _lookup.GetLocalEntitiesIntersecting(uid, offsetTile, gridComp: grid))
                        {
                            if (mobQuery.HasComp(localUid))
                            {
                                _damageSys.TryChangeDamage(localUid, mobDamage);

                                TransformComponent form = Transform(localUid);
                                if (!form.Anchored)
                                    _transform.Unanchor(localUid, form);
                                _throwing.TryThrow(localUid, dir * throwMultiplier);
                            }
                            else
                            {
                                // Проверяем наличие ExplosiveComponent и вызываем взрыв
                                if (explosiveQuery.TryGetComponent(localUid, out var explosive))
                                {
                                    _explosionSystem.TriggerExplosive(localUid, explosive, false);
                                }
                                else
                                {
                                    QueueDel(localUid);
                                }
                            }
                        }
                        _mapSys.SetTile(new Entity<MapGridComponent>(uid, grid), offsetTile, Tile.Empty);
                    }
                }
            }
        }

        if (energy > impact.SparkEnergy)
            SpawnAtPosition("EffectSparks", new EntityCoordinates(uid, tile));
    }
    /// <summary>
    /// При столкновении - вызывается функция оглушение, так же проверяется на то, что он был пристёгнут или же - что это моб
    /// </summary>
    private void StunMobsInTile(EntityUid gridUid, MapGridComponent grid, ShuttleImpactComponent impact)
    {
        var mobQuery = GetEntityQuery<MobStateComponent>();
        var xformQuery = GetEntityQuery<TransformComponent>();
        var buckleQuery = GetEntityQuery<BuckleComponent>();
        var ftlImmuneQuery = GetEntityQuery<FTLKnockdownImmuneComponent>();

        foreach (var entity in _lookup.GetEntitiesIntersecting(gridUid))
        {
            if (!Exists(entity))
                continue;

            if (!mobQuery.HasComponent(entity))
                continue;

            if (!xformQuery.TryGetComponent(entity, out var xform) || xform.GridUid != gridUid)
                continue;

            if (!buckleQuery.TryGetComponent(entity, out var buckle) || !buckle.Buckled)
            {
                if (!ftlImmuneQuery.HasComponent(entity))
                {
                    _stuns.TryParalyze(entity, TimeSpan.FromSeconds(impact.StunTime), true);
                }
            }
        }
    }

    /// <summary>
    /// Вспомогательный метод для определения тайлов, затронутых столкновением.
    /// </summary>
    private List<Vector2i> GetImpactedTiles(Vector2 point, Vector2 dir, int tileBreakLength, int tileBreakWidth)
    {
        var tiles = new List<Vector2i>();
        // Определяем угол столкновения
        var collisionAngle = MathF.Atan2(dir.Y, dir.X);
        // Исправлено: Используем MathF.Floor для каждого компонента
        var tile = new Vector2i((int)MathF.Floor(point.X), (int)MathF.Floor(point.Y));

        for (var i = 0; i < tileBreakLength; i++)
        {
            for (var j = -tileBreakWidth; j <= tileBreakWidth; j++)
            {
                // Смещаем тайл в зависимости от угла столкновения
                var offset = new Vector2(j, i);
                var rotatedOffset = new Vector2(
                    offset.X * MathF.Cos(collisionAngle) - offset.Y * MathF.Sin(collisionAngle),
                    offset.X * MathF.Sin(collisionAngle) + offset.Y * MathF.Cos(collisionAngle)
                );

                // Исправлено: Используем MathF.Round для каждого компонента
                var offsetTile = new Vector2i((int)MathF.Round(rotatedOffset.X), (int)MathF.Round(rotatedOffset.Y)) + tile;
                tiles.Add(offsetTile);
            }
        }

        return tiles;
    }

    /// <summary>
    /// Вспомогательный метод для расчета модификатора урона на основе угла столкновения.
    /// </summary>
    private float CalculateDamageMultiplier(Vector2 dir)
    {
        // Чем ближе к 1, тем больше урон (лобовое столкновение).
        // Чем ближе к 0, тем меньше урон (скользящее столкновение).
        return MathF.Abs(dir.X) + MathF.Abs(dir.Y);
    }

    /// <summary>
    /// Вспомогательный метод для расчета модификатора отбрасывания на основе угла столкновения.
    /// </summary>
    private float CalculateThrowMultiplier(Vector2 dir)
    {
        // Чем ближе к 1, тем больше отбрасывание (лобовое столкновение).
        // Чем ближе к 0, тем меньше отбрасывание (скользящее столкновение).
        return MathF.Abs(dir.X) + MathF.Abs(dir.Y);
    }
}
