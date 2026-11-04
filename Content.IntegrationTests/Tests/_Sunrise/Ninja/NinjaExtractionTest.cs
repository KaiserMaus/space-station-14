using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.Server.Antag.Components;
using Content.Server._Sunrise.TransitHub;
using Content.Server._Sunrise.Ninja.Components;
using Content.Server._Sunrise.Ninja.Systems;
using Content.Server.Objectives.Components;
using Content.Server.Pinpointer;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Ninja.Components;
using Content.Shared._Sunrise.Ninja.Events;
using Content.Shared.Chat;
using Content.Shared.DoAfter;
using Content.Shared.GameTicking;
using Content.Shared.Mind;
using Content.Shared.Ninja.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Pinpointer;
using Content.Shared.Random;
using Content.Shared.Shuttles.Components;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Reflection;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Sunrise.Ninja;

public sealed class NinjaExtractionTest : GameTest
{
    private static readonly ProtoId<WeightedRandomPrototype> FinalObjectiveGroups = "SunriseNinjaFinalObjectiveGroups";
    private static readonly ProtoId<WeightedRandomPrototype> FinalObjectiveGroup = "SunriseNinjaFinalObjectiveGroup";

    private const string NinjaPrototype = "NinjaExtractionTestMob";
    private const string ConsolePrototype = "NinjaExtractionTestConsole";
    private const string ObjectivePrototype = "NinjaExtractionTestObjective";
    private const string BeaconPrototype = "NinjaExtractionTestBeacon";
    private const string BeaconName = "Ninja Extraction Beacon";

    [TestPrototypes]
    private const string Prototypes = $$"""
- type: entity
  id: {{NinjaPrototype}}
  components:
  - type: DoAfter
  - type: MindContainer
  - type: SpaceNinja
  - type: NinjaPodHacker
    objective: {{ObjectivePrototype}}

- type: entity
  id: {{ConsolePrototype}}
  components:
  - type: Speech
    speechVerb: Robotic
  - type: PodConsole

- type: entity
  id: {{BeaconPrototype}}
  components:
  - type: NavMapBeacon
    text: {{BeaconName}}
  - type: ConfigurableNavMapBeacon

- type: entity
  id: {{ObjectivePrototype}}
  components:
  - type: Objective
    difficulty: 1
    issuer: SpiderClan
    icon:
      sprite: error.rsi
      state: error
  - type: CodeCondition
""";

    public override PoolSettings PoolSettings => new()
    {
        Connected = false,
        Dirty = true,
    };

    [Test]
    public async Task NinjaSpawnUsesWeightedFinalObjective()
    {
        EntityUid rule = default;

        await Pair.Server.WaitPost(() =>
        {
            rule = Pair.Server.EntMan.SpawnEntity("NinjaSpawn", MapCoordinates.Nullspace);
        });

        await Pair.Server.WaitAssertion(() =>
        {
            var fixedObjectives = Pair.Server.EntMan.GetComponent<AntagObjectivesComponent>(rule);
            var randomObjectives = Pair.Server.EntMan.GetComponent<AntagRandomObjectivesComponent>(rule);
            var groups = Pair.Server.ProtoMan.Index(FinalObjectiveGroups);
            var finalObjectives = Pair.Server.ProtoMan.Index(FinalObjectiveGroup);

            Assert.Multiple(() =>
            {
                Assert.That(fixedObjectives.Objectives, Does.Not.Contain("SunriseNinjaExtractObjective"));
                Assert.That(fixedObjectives.Objectives, Does.Not.Contain("NinjaSurviveObjective"));
                Assert.That(randomObjectives.Sets, Has.Count.EqualTo(1));
                Assert.That(randomObjectives.Sets[0].Groups.Id, Is.EqualTo(groups.ID));
                Assert.That(randomObjectives.Sets[0].MaxPicks, Is.EqualTo(1));
                Assert.That(randomObjectives.MaxDifficulty, Is.EqualTo(1.5f));
                Assert.That(groups.Weights, Is.EquivalentTo(new Dictionary<string, float>
                {
                    [finalObjectives.ID] = 1f,
                }));
                Assert.That(finalObjectives.Weights, Is.EquivalentTo(new Dictionary<string, float>
                {
                    ["SunriseNinjaExtractObjective"] = 4f,
                    ["NinjaSurviveObjective"] = 1f,
                }));
            });
        });
    }

    [Test]
    public async Task NinjaPodHackerUsesExtractionTimelineDefaults()
    {
        EntityUid ninja = default;

        await Pair.Server.WaitPost(() =>
        {
            ninja = Pair.Server.EntMan.SpawnEntity(NinjaPrototype, MapCoordinates.Nullspace);
        });

        await Pair.Server.WaitAssertion(() =>
        {
            var hacker = Pair.Server.EntMan.GetComponent<NinjaPodHackerComponent>(ninja);

            Assert.Multiple(() =>
            {
                Assert.That(hacker.LaunchDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
                Assert.That(hacker.MessageDelay, Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(hacker.NinjaRemovalDelay, Is.EqualTo(TimeSpan.FromSeconds(25)));
                Assert.That(hacker.PodRemovalDelay, Is.EqualTo(TimeSpan.FromSeconds(30)));
                Assert.That(hacker.FtlTravelTime, Is.EqualTo(60f));
            });
        });
    }

    [Test]
    public async Task CompletedHackSchedulesPodAndCompletesObjective()
    {
        var context = await SetupScenario(ConsolePrototype);
        var start = Pair.Server.ResolveDependency<IGameTiming>().CurTime;

        await Pair.Server.WaitPost(() => AttachStation(context, spawnBeacon: true));

        await Pair.Server.WaitAssertion(() =>
        {
            var entMan = Pair.Server.EntMan;
            var hacker = entMan.GetComponent<NinjaPodHackerComponent>(context.Ninja);
            var extractionSystem = Pair.Server.System<NinjaEscapePodExtractionSystem>();

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<TransformComponent>(context.Target).GridUid, Is.EqualTo(context.Grid));
                Assert.That(entMan.HasComponent<EscapePodComponent>(context.Grid), Is.True);
                Assert.That(entMan.HasComponent<ShuttleComponent>(context.Grid), Is.True);
                Assert.That(Pair.Server.System<StationSystem>().GetOwningStation(context.Target), Is.EqualTo(context.Station));
                Assert.That(
                    Pair.Server.System<NavMapSystem>().GetNearestBeaconString(
                        (context.Target, entMan.GetComponent<TransformComponent>(context.Target)),
                        onlyName: true),
                    Is.EqualTo(BeaconName));
                Assert.That(extractionSystem.CanScheduleExtraction(context.Ninja, context.Target, hacker), Is.True);
            });
        });

        await Pair.Server.WaitPost(() => StartExtraction(context.Ninja, context.Target));
        await Pair.Server.WaitRunTicks(1);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(context.Objective.Completed, Is.True);
                Assert.That(Pair.Server.EntMan.HasComponent<NinjaPodHackerComponent>(context.Ninja), Is.False);
                Assert.That(Pair.Server.EntMan.HasComponent<TimedDespawnComponent>(context.Ninja), Is.False);
                Assert.That(Pair.Server.EntMan.TryGetComponent<NinjaEscapePodExtractionComponent>(context.Grid, out var extraction), Is.True);
                Assert.That(extraction!.Ninja, Is.EqualTo(context.Ninja));
                Assert.That(extraction.Console, Is.EqualTo(context.Target));
                Assert.That(extraction.LaunchAt, Is.EqualTo(start + TimeSpan.FromSeconds(30)));
            });
        });

    }

    [Test]
    public async Task DuplicatePodExtractionIsRejected()
    {
        var context = await SetupScenario(ConsolePrototype);
        EntityUid secondNinja = default;
        CodeConditionComponent secondObjective = default!;

        await Pair.Server.WaitPost(() =>
        {
            (secondNinja, secondObjective) = SpawnNinjaWithObjective(
                new EntityCoordinates(context.Grid, Vector2.Zero));
        });

        await Pair.Server.WaitPost(() => StartExtraction(context.Ninja, context.Target));
        await Pair.Server.WaitRunTicks(1);
        await Pair.Server.WaitPost(() => StartExtraction(secondNinja, context.Target));
        await Pair.Server.WaitRunTicks(1);

        await Pair.Server.WaitAssertion(() =>
        {
            var extraction = Pair.Server.EntMan.GetComponent<NinjaEscapePodExtractionComponent>(context.Grid);
            Assert.Multiple(() =>
            {
                Assert.That(context.Objective.Completed, Is.True);
                Assert.That(secondObjective.Completed, Is.False);
                Assert.That(Pair.Server.EntMan.HasComponent<NinjaPodHackerComponent>(secondNinja), Is.True);
                Assert.That(extraction.Ninja, Is.EqualTo(context.Ninja));
            });
        });
    }

    [Test]
    public async Task CancelledExtractionDoesNothing()
    {
        var context = await SetupScenario(ConsolePrototype);

        await Pair.Server.WaitPost(() => StartExtraction(context.Ninja, context.Target, cancelled: true));
        await Pair.Server.WaitRunTicks(1);

        await AssertExtractionDidNothing(context);
    }

    [Test]
    public async Task ExtractionWithoutPodConsoleDoesNothing()
    {
        var context = await SetupScenario(null);

        await Pair.Server.WaitPost(() => StartExtraction(context.Ninja, context.Target));
        await Pair.Server.WaitRunTicks(1);

        await AssertExtractionDidNothing(context);
    }

    [Test]
    public async Task HackOutsideEscapePodDoesNothing()
    {
        var context = await SetupScenario(ConsolePrototype, createPodGrid: false);

        await Pair.Server.WaitPost(() => StartExtraction(context.Ninja, context.Target));
        await Pair.Server.WaitRunTicks(1);

        await AssertExtractionDidNothing(context);
    }

    [Test]
    public async Task InvalidPodDoesNotLaunch()
    {
        EntityUid invalidPod = default;

        await Pair.Server.WaitPost(() =>
        {
            invalidPod = Pair.Server.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var emergency = Pair.Server.System<EmergencyShuttleSystem>();
            Assert.That(emergency.TryLaunchEscapePod(invalidPod, 60f), Is.False);
        });
    }

    [Test]
    public async Task ScheduledPodLaunchesAtDeadline()
    {
        var context = await SetupScenario(ConsolePrototype);
        TimeSpan launchAt = default;

        await Pair.Server.WaitPost(() => AttachTransitHub(context));
        await Pair.Server.WaitPost(() => StartExtraction(context.Ninja, context.Target));
        await Pair.Server.WaitRunTicks(1);

        await Pair.Server.WaitPost(() =>
        {
            var server = Pair.Server;
            var extraction = server.EntMan.GetComponent<NinjaEscapePodExtractionComponent>(context.Grid);
            var extractionSystem = server.System<NinjaEscapePodExtractionSystem>();

            extractionSystem.Update(0f);
            Assert.That(server.EntMan.HasComponent<FTLComponent>(context.Grid), Is.False);

            launchAt = server.ResolveDependency<IGameTiming>().CurTime;
            extraction.LaunchAt = launchAt;
            extractionSystem.Update(0f);
        });
        await Pair.Server.WaitRunTicks(2);

        await Pair.Server.WaitAssertion(() =>
        {
            var extraction = Pair.Server.EntMan.GetComponent<NinjaEscapePodExtractionComponent>(context.Grid);
            Assert.Multiple(() =>
            {
                Assert.That(Pair.Server.EntMan.HasComponent<FTLComponent>(context.Grid), Is.True);
                Assert.That(Pair.Server.EntMan.HasComponent<EscapePodComponent>(context.Grid), Is.False);
                Assert.That(extraction.FtlStartedAt, Is.Not.Null);
                Assert.That(extraction.FtlStartedAt, Is.LessThan(launchAt + TimeSpan.FromSeconds(1)));
            });
        });
    }

    [Test]
    public async Task NonGridTransitHubDoesNotLaunch()
    {
        var context = await SetupScenario(ConsolePrototype);

        await Pair.Server.WaitPost(() =>
        {
            var server = Pair.Server;
            var station = AttachStation(context);
            var invalidDestination = server.EntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var transitHub = server.EntMan.EnsureComponent<StationTransitHubComponent>(station);
            typeof(StationTransitHubComponent)
                .GetField(nameof(StationTransitHubComponent.Entity))!
                .SetValue(transitHub, invalidDestination);

            Assert.That(server.System<EmergencyShuttleSystem>().TryLaunchEscapePod(context.Grid, 60f), Is.False);
            Assert.That(server.EntMan.HasComponent<FTLComponent>(context.Grid), Is.False);
        });
    }

    [Test]
    public async Task FailedFtlDoesNotArmFlightTimeline()
    {
        var context = await SetupScenario(ConsolePrototype);
        NinjaEscapePodExtractionComponent extraction = default!;

        await Pair.Server.WaitPost(() => StartExtraction(context.Ninja, context.Target));
        await Pair.Server.WaitRunTicks(1);
        await Pair.Server.WaitPost(() =>
        {
            var server = Pair.Server;
            extraction = server.EntMan.GetComponent<NinjaEscapePodExtractionComponent>(context.Grid);
            extraction.LaunchAt = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<NinjaEscapePodExtractionSystem>().Update(0f);
        });
        await Pair.Server.WaitRunTicks(1);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(extraction.FtlStartedAt, Is.Null);
                Assert.That(extraction.MessageAt, Is.Null);
                Assert.That(extraction.NinjaRemovalAt, Is.Null);
                Assert.That(extraction.PodRemovalAt, Is.Null);
                Assert.That(Pair.Server.EntMan.HasComponent<NinjaEscapePodExtractionComponent>(context.Grid), Is.False);
            });
        });
    }

    [TestCase("ninja")]
    [TestCase("console")]
    [TestCase("pod")]
    public async Task MissingExtractionEntityCancelsBeforeLaunch(string deletedEntity)
    {
        var context = await SetupScenario(ConsolePrototype);

        await Pair.Server.WaitPost(() => StartExtraction(context.Ninja, context.Target));
        await Pair.Server.WaitRunTicks(1);
        await Pair.Server.WaitPost(() =>
        {
            var server = Pair.Server;
            var extraction = server.EntMan.GetComponent<NinjaEscapePodExtractionComponent>(context.Grid);
            extraction.LaunchAt = server.ResolveDependency<IGameTiming>().CurTime;

            server.EntMan.DeleteEntity(deletedEntity switch
            {
                "ninja" => context.Ninja,
                "console" => context.Target,
                "pod" => context.Grid,
                _ => throw new ArgumentOutOfRangeException(nameof(deletedEntity)),
            });

            server.System<NinjaEscapePodExtractionSystem>().Update(0f);
        });
        await Pair.Server.WaitRunTicks(1);

        await Pair.Server.WaitAssertion(() =>
        {
            if (deletedEntity == "pod")
            {
                Assert.That(Pair.Server.EntMan.Deleted(context.Grid), Is.True);
                return;
            }

            Assert.Multiple(() =>
            {
                Assert.That(Pair.Server.EntMan.HasComponent<FTLComponent>(context.Grid), Is.False);
                Assert.That(Pair.Server.EntMan.HasComponent<NinjaEscapePodExtractionComponent>(context.Grid), Is.False);
            });
        });
    }

    [Test]
    public async Task FtlStartedArmsExactFlightDeadlines()
    {
        var context = await SetupScenario(ConsolePrototype);
        TimeSpan startedAt = default;

        await Pair.Server.WaitPost(() =>
        {
            var entMan = Pair.Server.EntMan;
            var timing = Pair.Server.ResolveDependency<IGameTiming>();
            var extraction = entMan.EnsureComponent<NinjaEscapePodExtractionComponent>(context.Grid);
            extraction.Ninja = context.Ninja;
            extraction.Console = context.Target;
            extraction.MessageDelay = TimeSpan.FromSeconds(10);
            extraction.NinjaRemovalDelay = TimeSpan.FromSeconds(25);
            extraction.PodRemovalDelay = TimeSpan.FromSeconds(30);
            startedAt = timing.CurTime;

            var ev = new FTLStartedEvent(
                context.Grid,
                new EntityCoordinates(context.Grid, Vector2.Zero),
                null,
                Matrix3x2.Identity,
                Angle.Zero);
            entMan.EventBus.RaiseLocalEvent(context.Grid, ref ev);
        });

        await Pair.Server.WaitAssertion(() =>
        {
            var extraction = Pair.Server.EntMan.GetComponent<NinjaEscapePodExtractionComponent>(context.Grid);
            Assert.Multiple(() =>
            {
                Assert.That(extraction.FtlStartedAt, Is.EqualTo(startedAt));
                Assert.That(extraction.MessageAt, Is.EqualTo(startedAt + TimeSpan.FromSeconds(10)));
                Assert.That(extraction.NinjaRemovalAt, Is.EqualTo(startedAt + TimeSpan.FromSeconds(25)));
                Assert.That(extraction.PodRemovalAt, Is.EqualTo(startedAt + TimeSpan.FromSeconds(30)));
            });
        });
    }

    [Test]
    public async Task FlightTimelineSpeaksThenDeletesNinjaBeforePod()
    {
        var context = await SetupScenario(ConsolePrototype);
        var server = Pair.Server;
        var effectsBefore = 0;
        var speechProbe = server.System<NinjaExtractionSpeechProbeSystem>();

        await server.WaitPost(() =>
        {
            speechProbe.Reset();
            var timing = server.ResolveDependency<IGameTiming>();
            var extraction = server.EntMan.EnsureComponent<NinjaEscapePodExtractionComponent>(context.Grid);
            extraction.Ninja = context.Ninja;
            extraction.Console = context.Target;
            extraction.FtlStartedAt = timing.CurTime;
            extraction.MessageAt = timing.CurTime + TimeSpan.FromHours(1);
            extraction.NinjaRemovalAt = timing.CurTime + TimeSpan.FromHours(1);
            extraction.PodRemovalAt = timing.CurTime + TimeSpan.FromHours(1);
            effectsBefore = CountPrototype("RedspaceImplantEffect");

            var extractionSystem = server.System<NinjaEscapePodExtractionSystem>();
            extractionSystem.Update(0f);
            Assert.That(extraction.MessageSent, Is.False);

            extraction.MessageAt = timing.CurTime;
            extractionSystem.Update(0f);
            Assert.Multiple(() =>
            {
                Assert.That(extraction.MessageSent, Is.True);
                Assert.That(speechProbe.ConsoleSpoke, Is.True);
                Assert.That(speechProbe.Speaker, Is.EqualTo(context.Target));
                Assert.That(speechProbe.SpokenMessage,
                    Does.Contain(server.EntMan.GetComponent<MetaDataComponent>(context.Ninja).EntityName));
            });

            extraction.NinjaRemovalAt = timing.CurTime;
            extractionSystem.Update(0f);
        });
        await Pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(server.EntMan.Deleted(context.Ninja), Is.True);
                Assert.That(server.EntMan.Deleted(context.Grid), Is.False);
                Assert.That(CountPrototype("RedspaceImplantEffect"), Is.EqualTo(effectsBefore + 1));
            });
        });

        await server.WaitPost(() =>
        {
            var extraction = server.EntMan.GetComponent<NinjaEscapePodExtractionComponent>(context.Grid);
            extraction.PodRemovalAt = server.ResolveDependency<IGameTiming>().CurTime;
            server.System<Content.Server._Sunrise.Ninja.Systems.NinjaEscapePodExtractionSystem>().Update(0f);
        });
        await Pair.RunTicksSync(2);

        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.Deleted(context.Grid), Is.True);
        });
    }

    [Test]
    public async Task EarlyNinjaDeletionStillDeletesPod()
    {
        var context = await SetupScenario(ConsolePrototype);

        await Pair.Server.WaitPost(() =>
        {
            var server = Pair.Server;
            var timing = server.ResolveDependency<IGameTiming>();
            var extraction = server.EntMan.EnsureComponent<NinjaEscapePodExtractionComponent>(context.Grid);
            extraction.Ninja = context.Ninja;
            extraction.Console = context.Target;
            extraction.FtlStartedAt = timing.CurTime;
            extraction.MessageAt = timing.CurTime + TimeSpan.FromHours(1);
            extraction.NinjaRemovalAt = timing.CurTime + TimeSpan.FromHours(1);
            extraction.PodRemovalAt = timing.CurTime;

            server.EntMan.DeleteEntity(context.Ninja);
            server.System<NinjaEscapePodExtractionSystem>().Update(0f);
        });
        await Pair.RunTicksSync(2);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Pair.Server.EntMan.Deleted(context.Grid), Is.True);
        });
    }

    [Test]
    public async Task EarlyConsoleDeletionDoesNotStopFlightTimeline()
    {
        var context = await SetupScenario(ConsolePrototype);

        await Pair.Server.WaitPost(() =>
        {
            var server = Pair.Server;
            var timing = server.ResolveDependency<IGameTiming>();
            var extraction = server.EntMan.EnsureComponent<NinjaEscapePodExtractionComponent>(context.Grid);
            extraction.Ninja = context.Ninja;
            extraction.Console = context.Target;
            extraction.FtlStartedAt = timing.CurTime;
            extraction.MessageAt = timing.CurTime;
            extraction.NinjaRemovalAt = timing.CurTime + TimeSpan.FromHours(1);
            extraction.PodRemovalAt = timing.CurTime + TimeSpan.FromHours(1);

            server.EntMan.DeleteEntity(context.Target);
            server.System<NinjaEscapePodExtractionSystem>().Update(0f);
            Assert.That(extraction.MessageSent, Is.True);

            extraction.PodRemovalAt = timing.CurTime;
            server.System<NinjaEscapePodExtractionSystem>().Update(0f);
        });
        await Pair.RunTicksSync(2);

        await Pair.Server.WaitAssertion(() =>
        {
            Assert.That(Pair.Server.EntMan.Deleted(context.Grid), Is.True);
        });
    }

    private async Task<Scenario> SetupScenario(string targetPrototype, bool createPodGrid = true)
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var mindSystem = server.System<SharedMindSystem>();
        var scenario = new Scenario();

        await server.WaitPost(() =>
        {
            if (createPodGrid)
            {
                var mapSystem = server.System<SharedMapSystem>();
                scenario.Map = mapSystem.CreateMap(out var mapId);
                var grid = mapSystem.CreateGridEntity(mapId);
                scenario.Grid = grid;
                mapSystem.SetTile(grid.Owner, grid.Comp, Vector2i.Zero, new Tile(1));
                entMan.EnsureComponent<EscapePodComponent>(scenario.Grid);
                entMan.EnsureComponent<ShuttleComponent>(scenario.Grid);
                scenario.Ninja = entMan.SpawnEntity(NinjaPrototype, new EntityCoordinates(scenario.Grid, Vector2.Zero));
                scenario.Target = entMan.SpawnEntity(targetPrototype, new EntityCoordinates(scenario.Grid, Vector2.Zero));
            }
            else
            {
                scenario.Ninja = entMan.SpawnEntity(NinjaPrototype, MapCoordinates.Nullspace);
                scenario.Target = entMan.SpawnEntity(targetPrototype, MapCoordinates.Nullspace);
            }

            var mind = mindSystem.CreateMind(null);
            mindSystem.TransferTo(mind, scenario.Ninja, mind: mind.Comp);
            Assert.That(mindSystem.TryAddObjective(mind, mind.Comp, ObjectivePrototype), Is.True);

            var objective = mind.Comp.Objectives.Single();
            scenario.Objective = entMan.GetComponent<CodeConditionComponent>(objective);
        });
        await server.WaitRunTicks(1);

        return scenario;
    }

    private void StartExtraction(EntityUid ninja, EntityUid target, bool cancelled = false)
    {
        var entMan = Pair.Server.EntMan;
        var timing = Pair.Server.ResolveDependency<IGameTiming>();
        Pair.Server.System<NinjaPodHackerSystem>();
        var doAfterSystem = Pair.Server.System<SharedDoAfterSystem>();
        var ev = new NinjaExtractionDoAfterEvent();
        var delay = cancelled ? timing.TickPeriod * 2 : TimeSpan.Zero;
        var args = new DoAfterArgs(entMan, ninja, delay, ev, ninja, target, ninja)
        {
            Broadcast = true,
        };

        Assert.That(doAfterSystem.TryStartDoAfter(args, out var id), Is.True);

        if (cancelled)
            doAfterSystem.Cancel(id);
    }

    private (EntityUid Ninja, CodeConditionComponent Objective) SpawnNinjaWithObjective(EntityCoordinates coordinates)
    {
        var server = Pair.Server;
        var ninja = server.EntMan.SpawnEntity(NinjaPrototype, coordinates);
        var mindSystem = server.System<SharedMindSystem>();
        var mind = mindSystem.CreateMind(null);
        mindSystem.TransferTo(mind, ninja, mind: mind.Comp);
        Assert.That(mindSystem.TryAddObjective(mind, mind.Comp, ObjectivePrototype), Is.True);

        var objective = mind.Comp.Objectives.Single();
        return (ninja, server.EntMan.GetComponent<CodeConditionComponent>(objective));
    }

    private EntityUid AttachStation(Scenario context, bool spawnBeacon = false)
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var station = entMan.SpawnEntity(null, MapCoordinates.Nullspace);
        context.Station = station;
        entMan.EnsureComponent<StationDataComponent>(station);
        server.System<StationSystem>().AddGridToStation(station, context.Grid);

        if (spawnBeacon)
            entMan.SpawnEntity(BeaconPrototype, new EntityCoordinates(context.Grid, Vector2.Zero));

        return station;
    }

    private void AttachTransitHub(Scenario context)
    {
        var server = Pair.Server;
        var entMan = server.EntMan;
        var mapSystem = server.System<SharedMapSystem>();
        var station = AttachStation(context);
        var destination = mapSystem.CreateGridEntity(server.Transform(context.Grid).MapID);
        mapSystem.SetTile(destination.Owner, destination.Comp, Vector2i.Zero, new Tile(1));
        var transitHub = entMan.EnsureComponent<StationTransitHubComponent>(station);
        typeof(StationTransitHubComponent)
            .GetField(nameof(StationTransitHubComponent.Entity))!
            .SetValue(transitHub, destination.Owner);
    }

    private int CountPrototype(string prototype)
    {
        return Pair.Server.EntMan.AllComponentsList<MetaDataComponent>()
            .Count(ent => ent.Component.EntityPrototype?.ID == prototype);
    }

    private async Task AssertExtractionDidNothing(Scenario context)
    {
        await Pair.Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(context.Objective.Completed, Is.False);
                Assert.That(Pair.Server.EntMan.HasComponent<NinjaPodHackerComponent>(context.Ninja), Is.True);
                Assert.That(Pair.Server.EntMan.HasComponent<TimedDespawnComponent>(context.Ninja), Is.False);
            });
        });
    }

    private sealed class Scenario
    {
        public EntityUid Ninja;
        public EntityUid Target;
        public EntityUid Map;
        public EntityUid Grid;
        public EntityUid Station;
        public CodeConditionComponent Objective = default!;
    }
}

[Reflect(false)]
public sealed partial class NinjaExtractionSpeechProbeSystem : EntitySystem
{
    public bool ConsoleSpoke;
    public EntityUid Speaker;
    public string SpokenMessage = string.Empty;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PodConsoleComponent, EntitySpokeEvent>(OnConsoleSpoke);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(_ => Reset());
    }

    public void Reset()
    {
        ConsoleSpoke = false;
        Speaker = default;
        SpokenMessage = string.Empty;
    }

    private void OnConsoleSpoke(Entity<PodConsoleComponent> ent, ref EntitySpokeEvent args)
    {
        ConsoleSpoke = true;
        Speaker = ent;
        SpokenMessage = args.Message;
    }
}
