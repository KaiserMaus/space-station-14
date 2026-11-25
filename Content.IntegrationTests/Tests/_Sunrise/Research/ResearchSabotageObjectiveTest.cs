using Content.IntegrationTests.Fixtures;
using Content.Server._Sunrise.Objectives.Components;
using Content.Server._Sunrise.Research.Events;
using Content.Server.Objectives;
using Content.Server.Objectives.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Random;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.Research;

#pragma warning disable RA0002 // Тест подготавливает изолированное состояние компонентов напрямую.
[TestFixture]
public sealed class ResearchSabotageObjectiveTest : GameTest
{
    private const string PrimaryTechnology = "SunriseTestObjectivePrimary";
    private const string SecondaryTechnology = "SunriseTestObjectiveSecondary";
    private static readonly ProtoId<WeightedRandomPrototype> OtherObjectiveGroup = "TraitorObjectiveGroupOther";
    private static readonly ProtoId<WeightedRandomPrototype> SabotageObjectiveGroup = "TraitorObjectiveGroupSabotage";

    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [TestPrototypes]
    private const string Prototypes = """
        - type: technology
          id: SunriseTestObjectivePrimary
          name: research-technology-sunrise-test-objective-primary
          icon:
            sprite: Interface/Misc/research_disciplines.rsi
            state: experimental
          discipline: Experimental
          tier: 1

        - type: technology
          id: SunriseTestObjectiveSecondary
          name: research-technology-sunrise-test-objective-secondary
          icon:
            sprite: Interface/Misc/research_disciplines.rsi
            state: experimental
          discipline: Experimental
          tier: 1
        """;

    [Test]
    public async Task TheftTargetUsesUniqueTechnologyProgress()
    {
        var objectives = Server.System<ObjectivesSystem>();
        EntityUid objectiveUid = default;
        EntityUid mindUid = default;
        MindComponent mind = default!;
        int target = 0;

        await Server.WaitPost(() =>
        {
            mindUid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            mind = SEntMan.AddComponent<MindComponent>(mindUid);
            objectiveUid = SEntMan.SpawnEntity("SunriseStealResearchDataObjective", MapCoordinates.Nullspace);

            var assigned = new ObjectiveAssignedEvent(mindUid, mind);
            SEntMan.EventBus.RaiseLocalEvent(objectiveUid, ref assigned);

            target = SEntMan.GetComponent<NumberObjectiveComponent>(objectiveUid).Target;
            var condition = SEntMan.GetComponent<StealResearchDataConditionComponent>(objectiveUid);
            condition.DownloadedTechnologies.Add(PrimaryTechnology);
            condition.DownloadedTechnologies.Add(PrimaryTechnology);
            condition.DownloadedTechnologies.Add(SecondaryTechnology);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(target, Is.InRange(3, 10));
                Assert.That(objectives.GetProgress(objectiveUid, (mindUid, mind)),
                    Is.EqualTo(2f / target).Within(0.0001f));
            });
        });
    }

    [Test]
    public async Task RequirementDistinguishesLimitedAndFullStationServers()
    {
        var map = await Pair.CreateTestMap();
        EntityUid theftRequirementUid = default;
        EntityUid overheatRequirementUid = default;
        EntityUid mindUid = default;
        EntityUid serverUid = default;
        MindComponent mind = default!;

        await Server.WaitPost(() =>
        {
            mindUid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            mind = SEntMan.AddComponent<MindComponent>(mindUid);

            theftRequirementUid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var theftRequirement = SEntMan.AddComponent<ResearchServerObjectiveRequirementComponent>(theftRequirementUid);
            theftRequirement.AllowLimitedServers = true;

            overheatRequirementUid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<ResearchServerObjectiveRequirementComponent>(overheatRequirementUid);

            var stationUid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var stationMember = SEntMan.AddComponent<StationMemberComponent>(map.Grid.Owner);
            stationMember.Station = stationUid;

            serverUid = SEntMan.SpawnEntity(null, map.GridCoords);
            SEntMan.AddComponent<ResearchServerComponent>(serverUid);
            var database = SEntMan.AddComponent<TechnologyDatabaseComponent>(serverUid);
            database.MaximumTechnologyTier = 2;

            Assert.Multiple(() =>
            {
                Assert.That(CheckRequirement(theftRequirementUid, mindUid, mind), Is.True);
                Assert.That(CheckRequirement(overheatRequirementUid, mindUid, mind), Is.False);
            });

            database.MaximumTechnologyTier = null;
            Assert.That(CheckRequirement(overheatRequirementUid, mindUid, mind), Is.True);

            SEntMan.RemoveComponent<StationMemberComponent>(map.Grid.Owner);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(CheckRequirement(theftRequirementUid, mindUid, mind), Is.False);
                Assert.That(CheckRequirement(overheatRequirementUid, mindUid, mind), Is.False);
            });
        });
    }

    [Test]
    public async Task OverheatStartCompletesEveryExistingObjective()
    {
        var objectives = Server.System<ObjectivesSystem>();
        EntityUid firstObjective = default;
        EntityUid secondObjective = default;
        EntityUid mindUid = default;
        MindComponent mind = default!;

        await Server.WaitPost(() =>
        {
            mindUid = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            mind = SEntMan.AddComponent<MindComponent>(mindUid);
            firstObjective = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            secondObjective = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<OverheatResearchServerConditionComponent>(firstObjective);
            SEntMan.AddComponent<OverheatResearchServerConditionComponent>(secondObjective);

            var started = new ResearchServerOverheatStartedEvent(EntityUid.Invalid);
            SEntMan.EventBus.RaiseEvent(EventSource.Local, ref started);
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(objectives.GetProgress(firstObjective, (mindUid, mind)), Is.EqualTo(1f));
                Assert.That(objectives.GetProgress(secondObjective, (mindUid, mind)), Is.EqualTo(1f));
            });
        });
    }

    [Test]
    public async Task ObjectivePrototypesAreMutuallyExclusiveAndWeighted()
    {
        string[] theftBlacklist = [];
        string[] overheatBlacklist = [];
        float theftWeight = 0;
        float overheatWeight = 0;

        await Server.WaitPost(() =>
        {
            var theft = SEntMan.SpawnEntity("SunriseStealResearchDataObjective", MapCoordinates.Nullspace);
            var overheat = SEntMan.SpawnEntity("SunriseOverheatResearchServerObjective", MapCoordinates.Nullspace);
            theftBlacklist = SEntMan.GetComponent<ObjectiveBlacklistRequirementComponent>(theft).Blacklist.Components;
            overheatBlacklist = SEntMan.GetComponent<ObjectiveBlacklistRequirementComponent>(overheat).Blacklist.Components;
            var otherGroup = SProtoMan.Index(OtherObjectiveGroup);
            var sabotageGroup = SProtoMan.Index(SabotageObjectiveGroup);
            theftWeight = otherGroup.Weights["SunriseStealResearchDataObjective"];
            overheatWeight = sabotageGroup.Weights["SunriseOverheatResearchServerObjective"];
        });

        await Server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(theftBlacklist, Does.Contain("OverheatResearchServerCondition"));
                Assert.That(overheatBlacklist, Does.Contain("StealResearchDataCondition"));
                Assert.That(theftWeight, Is.EqualTo(1));
                Assert.That(overheatWeight, Is.EqualTo(0.1f));
            });
        });
    }

    private bool CheckRequirement(EntityUid requirementUid, EntityUid mindUid, MindComponent mind)
    {
        var ev = new RequirementCheckEvent(mindUid, mind);
        SEntMan.EventBus.RaiseLocalEvent(requirementUid, ref ev);
        return !ev.Cancelled;
    }
}
