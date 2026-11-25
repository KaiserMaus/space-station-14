using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Tests.Helpers;
using Content.Server.Research.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Research.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.Research;

#pragma warning disable RA0002 // Тест подготавливает изолированное состояние компонента напрямую.
[TestFixture]
public sealed class ResearchTheftTest : GameTest
{
    private sealed class DatabaseModifiedListenerSystem : TestListenerSystem<TechnologyDatabaseModifiedEvent>;
    private sealed class ResearchStolenListenerSystem : TestListenerSystem<ResearchStolenEvent>;

    private const string PrimaryTechnology = "SunriseTestTheftPrimary";
    private const string SharedTechnology = "SunriseTestTheftShared";
    private const string PrerequisiteTechnology = "SunriseTestTheftPrerequisite";
    private const string FinalTechnology = "SunriseTestTheftFinal";
    private static readonly ProtoId<LatheRecipePrototype> SharedRecipe = "ProtolatheMachineCircuitboard";
    private static readonly ProtoId<LatheRecipePrototype> UniqueRecipe = "AutolatheMachineCircuitboard";
    private static readonly ProtoId<LatheRecipePrototype> FinalRecipe = "AutolatheHyperConvectionMachineCircuitboard";

    public override PoolSettings PoolSettings => new() { Connected = false };

    [TestPrototypes]
    private const string Prototypes = """
        - type: technology
          id: SunriseTestTheftPrimary
          name: research-technology-sunrise-test-theft-primary
          icon:
            sprite: Interface/Misc/research_disciplines.rsi
            state: experimental
          discipline: Experimental
          tier: 1
          recipeUnlocks:
          - ProtolatheMachineCircuitboard
          - AutolatheMachineCircuitboard

        - type: technology
          id: SunriseTestTheftShared
          name: research-technology-sunrise-test-theft-shared
          icon:
            sprite: Interface/Misc/research_disciplines.rsi
            state: experimental
          discipline: Experimental
          tier: 1
          recipeUnlocks:
          - ProtolatheMachineCircuitboard

        - type: technology
          id: SunriseTestTheftPrerequisite
          name: research-technology-sunrise-test-theft-prerequisite
          icon:
            sprite: Interface/Misc/research_disciplines.rsi
            state: experimental
          discipline: Experimental
          tier: 2
          technologyPrerequisites:
          - SunriseTestTheftPrimary

        - type: technology
          id: SunriseTestTheftFinal
          name: research-technology-sunrise-test-theft-final
          icon:
            sprite: Interface/Misc/research_disciplines.rsi
            state: experimental
          discipline: Experimental
          tier: 1
          requiresResearchCompletion: true
          recipeUnlocks:
          - AutolatheHyperConvectionMachineCircuitboard
        """;

    [Test]
    public async Task StolenTechnologyPreservesHistoryAndPrerequisites()
    {
        var research = Server.System<ResearchSystem>();
        var prerequisite = SProtoMan.Index<TechnologyPrototype>(PrerequisiteTechnology);
        EntityUid databaseUid = default;

        await Server.WaitPost(() =>
        {
            databaseUid = SpawnDatabase([PrimaryTechnology], [SharedRecipe, UniqueRecipe]);
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);

            Assert.That(research.TryStealTechnology((databaseUid, database), PrimaryTechnology), Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);
            var tiers = new Dictionary<string, int> { ["Experimental"] = 2 };

            Assert.Multiple(() =>
            {
                Assert.That(database.UnlockedTechnologies, Does.Contain(PrimaryTechnology));
                Assert.That(database.StolenTechnologies, Does.Contain(PrimaryTechnology));
                Assert.That(database.UnlockedRecipes, Does.Not.Contain(SharedRecipe));
                Assert.That(database.UnlockedRecipes, Does.Not.Contain(UniqueRecipe));
                Assert.That(research.IsTechnologyAvailable(database, prerequisite, tiers), Is.True);
            });
        });
    }

    [Test]
    public async Task FinalAndDuplicateTechnologyCannotBeStolen()
    {
        var research = Server.System<ResearchSystem>();
        EntityUid databaseUid = default;

        await Server.WaitPost(() =>
        {
            databaseUid = SpawnDatabase([PrimaryTechnology, FinalTechnology], [SharedRecipe, UniqueRecipe]);
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);

            Assert.Multiple(() =>
            {
                Assert.That(research.TryStealTechnology((databaseUid, database), FinalTechnology), Is.False);
                Assert.That(research.TryStealTechnology((databaseUid, database), PrimaryTechnology), Is.True);
                Assert.That(research.TryStealTechnology((databaseUid, database), PrimaryTechnology), Is.False);
            });
        });

        await Server.WaitAssertion(() =>
        {
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);
            Assert.Multiple(() =>
            {
                Assert.That(database.StolenTechnologies, Is.EqualTo(new[] { PrimaryTechnology }));
                Assert.That(database.UnlockedTechnologies, Does.Contain(FinalTechnology));
            });
        });
    }

    [Test]
    public async Task SharedRecipeSurvivesOtherTechnologyTheft()
    {
        var research = Server.System<ResearchSystem>();
        EntityUid databaseUid = default;

        await Server.WaitPost(() =>
        {
            databaseUid = SpawnDatabase(
                [PrimaryTechnology, SharedTechnology],
                [SharedRecipe, UniqueRecipe]);
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);

            Assert.That(research.TryStealTechnology((databaseUid, database), PrimaryTechnology), Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);
            Assert.Multiple(() =>
            {
                Assert.That(database.UnlockedRecipes, Does.Contain(SharedRecipe));
                Assert.That(database.UnlockedRecipes, Does.Not.Contain(UniqueRecipe));
            });
        });
    }

    [Test]
    public async Task DiskRestoredRecipeSurvivesLaterTheft()
    {
        var research = Server.System<ResearchSystem>();
        EntityUid databaseUid = default;

        await Server.WaitPost(() =>
        {
            databaseUid = SpawnDatabase(
                [PrimaryTechnology, SharedTechnology],
                [SharedRecipe, UniqueRecipe]);
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);

            research.AddTechnologyDiskRecipe((databaseUid, database), SharedRecipe);
            Assert.That(research.TryStealTechnology((databaseUid, database), PrimaryTechnology), Is.True);
            Assert.That(research.TryStealTechnology((databaseUid, database), SharedTechnology), Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);
            Assert.Multiple(() =>
            {
                Assert.That(database.DiskRestoredRecipes, Does.Contain(SharedRecipe));
                Assert.That(database.UnlockedRecipes, Does.Contain(SharedRecipe));
                Assert.That(database.StolenTechnologies, Does.Contain(PrimaryTechnology));
                Assert.That(database.StolenTechnologies, Does.Contain(SharedTechnology));
            });
        });
    }

    [Test]
    public async Task ConnectedLatheUpdatesImmediatelyAfterTheft()
    {
        var research = Server.System<ResearchSystem>();
        var listener = Server.System<DatabaseModifiedListenerSystem>();
        EntityUid serverUid = default;
        EntityUid latheUid = default;

        await Server.WaitPost(() =>
        {
            serverUid = SpawnDatabase([PrimaryTechnology], [SharedRecipe, UniqueRecipe]);
            SEntMan.AddComponent<ResearchServerComponent>(serverUid);

            latheUid = SEntMan.SpawnEntity("Protolathe", MapCoordinates.Nullspace);
            SEntMan.AddComponent<TestListenerComponent>(latheUid);
            research.RegisterClient(latheUid, serverUid);
            listener.Clear(latheUid);

            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(serverUid);
            Assert.That(research.TryStealTechnology((serverUid, database), PrimaryTechnology), Is.True);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.That(listener.Count(latheUid), Is.EqualTo(1));
        });
    }

    [Test]
    public async Task NinjaTheftSkipsStolenAndFinalTechnologies()
    {
        var listener = Server.System<ResearchStolenListenerSystem>();
        EntityUid stealerUid = default;
        EntityUid databaseUid = default;

        await Server.WaitPost(() =>
        {
            stealerUid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var stealer = SEntMan.AddComponent<ResearchStealerComponent>(stealerUid);
            stealer.MinToSteal = 10;
            stealer.MaxToSteal = 10;
            SEntMan.AddComponent<TestListenerComponent>(stealerUid);

            databaseUid = SpawnDatabase(
                [PrimaryTechnology, SharedTechnology, FinalTechnology],
                [SharedRecipe, FinalRecipe]);
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);
            database.StolenTechnologies.Add(PrimaryTechnology);

            var doAfterEvent = new ResearchStealDoAfterEvent();
            var doAfterArgs = new DoAfterArgs(
                SEntMan,
                stealerUid,
                TimeSpan.Zero,
                doAfterEvent,
                target: databaseUid,
                eventTarget: stealerUid);
            doAfterEvent.DoAfter = new Content.Shared.DoAfter.DoAfter(0, doAfterArgs, TimeSpan.Zero);
            SEntMan.EventBus.RaiseLocalEvent(stealerUid, doAfterEvent);
        });

        await Server.WaitAssertion(() =>
        {
            var database = SEntMan.GetComponent<TechnologyDatabaseComponent>(databaseUid);
            var stolenEvent = listener.GetEvents(stealerUid).Single();

            Assert.Multiple(() =>
            {
                Assert.That(stolenEvent.Techs, Is.EqualTo(new[] { SharedTechnology }));
                Assert.That(database.StolenTechnologies, Is.EquivalentTo(new[] { PrimaryTechnology, SharedTechnology }));
                Assert.That(database.UnlockedTechnologies,
                    Is.EquivalentTo(new[] { PrimaryTechnology, SharedTechnology, FinalTechnology }));
                Assert.That(database.UnlockedRecipes, Does.Not.Contain(SharedRecipe));
                Assert.That(database.UnlockedRecipes, Does.Contain(FinalRecipe));
            });
        });
    }

    private EntityUid SpawnDatabase(
        IReadOnlyCollection<ProtoId<TechnologyPrototype>> technologies,
        IReadOnlyCollection<ProtoId<LatheRecipePrototype>> recipes)
    {
        var uid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
        var database = SEntMan.AddComponent<TechnologyDatabaseComponent>(uid);
        database.SupportedDisciplines.Add("Experimental");
        database.UnlockedTechnologies.AddRange(technologies);
        database.UnlockedRecipes.AddRange(recipes);
        return uid;
    }
}
