using System.Collections.Generic;
using System.Linq;
using Content.Server.Cargo.Systems;
using Content.Shared._NF.Shipyard;
using Content.Shared._NF.Shipyard.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._NF;

[TestFixture]
public sealed class ShipyardTest
{
    [Test]
    public async Task CheckAllShuttleGrids()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var mapLoader = entManager.System<MapLoaderSystem>();
        var map = entManager.System<MapSystem>();

        await server.WaitPost(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var vessel in protoManager.EnumeratePrototypes<VesselPrototype>())
                {
                    map.CreateMap(out var mapId);

                    bool mapLoaded = false;
                    Entity<MapGridComponent>? shuttle = null;
                    try
                    {
                        mapLoaded = mapLoader.TryLoadGrid(mapId, vessel.ShuttlePath, out shuttle);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Failed to load shuttle {vessel} ({vessel.ShuttlePath}): TryLoadGrid threw exception {ex}");
                        map.DeleteMap(mapId);
                        continue;
                    }

                    Assert.That(mapLoaded, Is.True, $"Failed to load shuttle {vessel} ({vessel.ShuttlePath}): TryLoadGrid returned false.");
                    Assert.That(shuttle.HasValue, Is.True);
                    Assert.That(entManager.HasComponent<MapGridComponent>(shuttle.Value), Is.True);

                    try
                    {
                        map.DeleteMap(mapId);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Failed to delete map for {vessel} ({vessel.ShuttlePath}): {ex}");
                    }
                }
            });
        });
        await server.WaitRunTicks(1);
        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task NoShipyardShipArbitrage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;

        var entManager = server.ResolveDependency<IEntityManager>();
        var mapLoader = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<MapLoaderSystem>();
        var map = entManager.System<MapSystem>();
        var protoManager = server.ResolveDependency<IPrototypeManager>();
        var pricing = server.ResolveDependency<IEntitySystemManager>().GetEntitySystem<PricingSystem>();

        var factionMarkups = new Dictionary<ShipyardConsoleUiKey, float>()
        {
            { ShipyardConsoleUiKey.Security, 1.35f },
            { ShipyardConsoleUiKey.Syndicate, 1.5f },
            { ShipyardConsoleUiKey.BlackMarket, 1.4f },
            { ShipyardConsoleUiKey.Mercenary, 1.3f },
            { ShipyardConsoleUiKey.Medical, 1.3f },
            { ShipyardConsoleUiKey.Scrap, 1.15f }
        };
    
        const float defaultMarkup = 1.2f;

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var vessel in protoManager.EnumeratePrototypes<VesselPrototype>())
                {
                    map.CreateMap(out var mapId);
                    double appraisePrice = 0;

                    bool mapLoaded = false;
                    Entity<MapGridComponent>? shuttle = null;
                    try
                    {
                        mapLoaded = mapLoader.TryLoadGrid(mapId, vessel.ShuttlePath, out shuttle);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Failed to load shuttle {vessel} ({vessel.ShuttlePath}): TryLoadGrid threw exception {ex}");
                        map.DeleteMap(mapId);
                        continue;
                    }
                    Assert.That(mapLoaded, Is.True, $"Failed to load shuttle {vessel} ({vessel.ShuttlePath}): TryLoadGrid returned false.");
                    Assert.That(entManager.HasComponent<MapGridComponent>(shuttle.Value), Is.True);

                    if (!mapLoaded)
                        continue;

                    pricing.AppraiseGrid(shuttle.Value, null, (uid, price) =>
                    {
                        appraisePrice += price;
                    });
                    
                    var shipyardConsole = vessel.Group;
                    float minMarkup;
                    
                    if (factionMarkups.TryGetValue(shipyardConsole, out var factionMarkup))
                    {
                        minMarkup = factionMarkup;
                    }
                    else
                    {
                        minMarkup = defaultMarkup;
                        Console.WriteLine($"Faction '{shipyardConsole}' not found in markup dictionary. Using default markup: {defaultMarkup}");
                    }
                    
                    var idealMinPrice = appraisePrice * minMarkup;
                    var markupPercent = (minMarkup - 1.0f) * 100;

                    if (!vessel.mapcheckerException)
                    {
                        Assert.That(vessel.Price, Is.AtLeast(idealMinPrice),
                                    $"Arbitrage possible on {vessel.ID}. " +
                                    $"Minimal price should be {idealMinPrice:F2}, " +
                                    $"{markupPercent:F1}% over the appraise price ({appraisePrice:F2}). " +
                                    $"Faction: {shipyardConsole} " +
                                    $"(Markup: {minMarkup:F2}, Prototype MinPriceMarkup: {vessel.MinPriceMarkup:F2})");
                    }

                    map.DeleteMap(mapId);
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
