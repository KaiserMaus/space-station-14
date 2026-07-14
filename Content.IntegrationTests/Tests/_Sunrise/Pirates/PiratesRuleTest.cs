using System.Collections.Generic;
using Content.Server._Sunrise.Pirates.GameTicking;
using Content.Server.Cargo.Systems;
using Content.Server.GameTicking;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Pirate;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.Pirates;

[TestFixture]
public sealed class PiratesRuleTest
{
    private const string PiratesRulePrototype = "Pirates";
    private const string PirateCaptainSpawnerPrototype = "SpawnPointPirateCaptain";
    private const string PirateCrewSpawnerPrototype = "SpawnPointPirateCrew";
    private const string PirateOrderConsolePrototype = "ComputerCargoOrdersPirate";

    private static readonly Dictionary<ProtoId<CargoProductPrototype>, (int Cost, ProtoId<CargoMarketPrototype> Market)> ExpectedProducts = new()
    {
        ["PirateMarketMagazineGrenadeFragTimer"] = (2500, "pirateMarketFreelance"),
        ["PirateMarketMagazineGrenadeBlastTimer"] = (2500, "pirateMarketFreelance"),
        ["PirateMarketMagazineGrenadeEMPTimer"] = (2500, "pirateMarketFreelance"),
        ["PirateMarketMagazineGrenadeFragContact"] = (3750, "pirateMarketFreelance"),
        ["PirateMarketMagazineGrenadeBlastContact"] = (3750, "pirateMarketFreelance"),
        ["PirateMarketMagazineGrenadeEMPContact"] = (3750, "pirateMarketFreelance"),
        ["PirateMarketMagazineGrenadeFragExtended"] = (5625, "pirateMarketFreelance"),
        ["PirateMarketMagazineGrenadeBlastExtended"] = (5625, "pirateMarketFreelance"),
        ["PirateMarketMagazineGrenadeEMPExtended"] = (5625, "pirateMarketFreelance"),
        ["PirateMarketShuttleGunKineticFlatpack"] = (750, "pirateMarket"),
        ["PirateMarketComputerGunneryCircuitboard"] = (1000, "pirateMarket"),
        ["PirateMarketSyndicateShuttleConsoleCircuitboard"] = (1000, "pirateMarket"),
        ["PirateMarketShipCannon"] = (3000, "pirateMarketPirate"),
        ["PirateMarketSyndicateShuttleBundle"] = (2750, "pirateMarketPirate"),
        ["PirateMarketSyndicateFriendshipBundle"] = (4000, "pirateMarketFreelance"),
        ["PirateMarketSyndicateLongbowBundle"] = (4500, "pirateMarketFreelance"),
        ["PirateMarketSyndicateBullfrogBundle"] = (6000, "pirateMarketBaron"),
        ["PirateMarketSyndicateQuadlingBundle"] = (5500, "pirateMarketBaron"),
        ["PirateMarketC4Bundle"] = (7750, "pirateMarketBaron"),
        ["PirateMarketMedsBundle"] = (15500, "pirateMarketBaron"),
        ["PirateMarketEmag"] = (2500, "pirateMarketBaron"),
        ["PirateMarketSyndicateWeaponModule"] = (2500, "pirateMarketBaron"),
        ["PirateMarketToolbox"] = (1500, "pirateMarketBaron"),
        ["PirateMarketSyndicateJawsOfLife"] = (1000, "pirateMarketBaron"),
        ["PirateMarketDuffelSurgery"] = (750, "pirateMarketBaron"),
        ["PirateMarketSyndieIntellicard"] = (2000, "pirateMarketBaron"),
        ["PirateMarketBootsMagSyndie"] = (2500, "pirateMarketBaron"),
        ["PirateMarketSyndicateSurplusBundle"] = (50000, "pirateMarketBaron"),
        ["PirateMarketAccessBreakerLimited"] = (2000, "pirateMarketPirate"),
        ["PirateMarketIK30"] = (6000, "pirateMarketFreelance"),
        ["PirateMarketMagazineBatteryLr60"] = (3000, "pirateMarketFreelance"),
        ["PirateMarketAccessBreaker"] = (2250, "pirateMarketFreelance"),
        ["PirateMarketChestRigFilled"] = (5000, "pirateMarketFreelance"),
        ["PirateMarketClothingEyesGlassesThermal"] = (2500, "pirateMarketFreelance"),
        ["PirateMarketSovietArmorVestCrate"] = (5000, "pirateMarket"),
        ["PirateMarketComputerPirateUplinkFlatpack"] = (5000, "pirateMarketPirate"),
        ["PirateMarketComputerPirateExchangerFlatpack"] = (3000, "pirateMarketFreelance"),
    };

    private static readonly HashSet<ProtoId<CargoMarketPrototype>> PirateMarkets =
    [
        "pirateMarket",
        "pirateMarketPirate",
        "pirateMarketFreelance",
        "pirateMarketBaron",
    ];

    [Test]
    public async Task StartingRuleInitializesPirateBase()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            Dirty = true,
            DummyTicker = false,
            InLobby = true,
        });

        var server = pair.Server;
        var ticker = server.System<GameTicker>();
        var cargo = server.System<CargoSystem>();
        var station = server.System<StationSystem>();
        EntityUid rule = default;

        await server.WaitPost(() =>
        {
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });

        await pair.RunTicksSync(10);

        await server.WaitPost(() => ticker.StartGameRule(PiratesRulePrototype, out rule));
        await pair.RunTicksSync(10);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.TryGetComponent<PiratesRuleComponent>(rule, out var piratesRule), Is.True);
            Assert.That(piratesRule!.AssociatedStation, Is.Not.EqualTo(EntityUid.Invalid));

            EntityUid? pirateGrid = null;
            Entity<CargoOrderConsoleComponent>? orderConsole = null;
            var captainSpawners = 0;
            var crewSpawners = 0;

            var query = server.EntMan.EntityQueryEnumerator<MetaDataComponent>();
            while (query.MoveNext(out var uid, out var meta))
            {
                if (server.EntMan.HasComponent<PirateBaseComponent>(uid))
                    pirateGrid = uid;

                switch (meta.EntityPrototype?.ID)
                {
                    case PirateCaptainSpawnerPrototype:
                        captainSpawners++;
                        break;
                    case PirateCrewSpawnerPrototype:
                        crewSpawners++;
                        break;
                    case PirateOrderConsolePrototype when server.EntMan.TryGetComponent<CargoOrderConsoleComponent>(uid, out var console):
                        orderConsole = (uid, console);
                        break;
                }
            }

            Assert.That(pirateGrid, Is.Not.Null);
            Assert.That(station.GetOwningStation(pirateGrid!.Value), Is.EqualTo(piratesRule.AssociatedStation));
            Assert.That(orderConsole, Is.Not.Null);
            Assert.That(station.GetOwningStation(orderConsole!.Value), Is.EqualTo(piratesRule.AssociatedStation));
            Assert.That(cargo.GetAvailableProducts(orderConsole.Value), Is.Not.Empty);
            Assert.That(captainSpawners, Is.EqualTo(1));
            Assert.That(crewSpawners, Is.GreaterThanOrEqualTo(2));
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task PirateMarketPricesPreventResaleArbitrage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var testMap = await pair.CreateTestMap();
        var pricing = server.System<PricingSystem>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var (id, expected) in ExpectedProducts)
                {
                    Assert.That(server.ProtoMan.TryIndex(id, out CargoProductPrototype product), Is.True,
                        $"Pirate cargo product {id} is missing.");

                    if (product is null)
                        continue;

                    Assert.That(product.Cost, Is.EqualTo(expected.Cost),
                        $"Pirate cargo product {id} has an unexpected purchase price.");
                    Assert.That(product.Group, Is.EqualTo(expected.Market),
                        $"Pirate cargo product {id} is available at an unexpected rank.");
                }

                foreach (var product in server.ProtoMan.EnumeratePrototypes<CargoProductPrototype>())
                {
                    if (!PirateMarkets.Contains(product.Group))
                        continue;

                    // Лотерейный ящик намеренно может содержать случайный выигрыш выше цены покупки.
                    if (product.Product == "CrateCargoGambling")
                        continue;

                    var entity = server.EntMan.SpawnEntity(product.Product, testMap.MapCoords);
                    var salePrice = pricing.GetPrice(entity);

                    Assert.That(salePrice, Is.AtMost(product.Cost),
                        $"Pirate cargo product {product.ID} can be resold for {salePrice}, but costs {product.Cost}.");

                    server.EntMan.DeleteEntity(entity);
                }
            });
        });

        await pair.CleanReturnAsync();
    }
}
