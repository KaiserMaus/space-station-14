using Content.IntegrationTests.Fixtures;
using Content.Server._Sunrise.Objectives.Components;
using Content.Server._Sunrise.Research.Components;
using Content.Server._Sunrise.Research.Systems;
using Content.Server.Power.Components;
using Content.Shared._Sunrise.Research.Events;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._Sunrise.Research;

#pragma warning disable RA0002 // Тест подготавливает изолированное состояние компонентов напрямую.
[TestFixture]
public sealed class ResearchDataSiphonTest : GameTest
{
    public override PoolSettings PoolSettings => new() { Connected = false, Dirty = true };

    [Test]
    public async Task ActivationRequiresSafePoweredLargestStationGridAndPreservesBinding()
    {
        var map = await Pair.CreateTestMap();
        var siphon = Server.System<ResearchDataSiphonSystem>();
        var transform = Server.System<SharedTransformSystem>();
        EntityUid beacon = default;
        EntityUid firstUser = default;
        EntityUid secondUser = default;
        EntityUid firstMind = default;
        EntityUid objective = default;

        await Server.WaitPost(() =>
        {
            FillFloor(map.Grid);
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var stationData = SEntMan.AddComponent<StationDataComponent>(station);
            stationData.Grids.Add(map.Grid.Owner);
            var stationMember = SEntMan.AddComponent<StationMemberComponent>(map.Grid.Owner);
            stationMember.Station = station;

            firstUser = SEntMan.SpawnEntity(null, map.GridCoords);
            secondUser = SEntMan.SpawnEntity(null, map.GridCoords);
            firstMind = AttachMind(firstUser, out var firstMindComp);
            AttachMind(secondUser, out _);
            objective = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StealResearchDataConditionComponent>(objective);
            firstMindComp.Objectives.Add(objective);

            beacon = SEntMan.SpawnEntity("SunriseResearchDataSiphon", map.GridCoords);
            transform.AnchorEntity(beacon);
            SEntMan.GetComponent<ApcPowerReceiverComponent>(beacon).Powered = true;

            Assert.That(siphon.CanActivate(beacon, firstUser), Is.True);
            Assert.That(siphon.TryActivate(beacon, firstUser), Is.True);
            var component = SEntMan.GetComponent<ResearchDataSiphonComponent>(beacon);
            Assert.Multiple(() =>
            {
                Assert.That(component.BoundMind, Is.EqualTo(firstMind));
                Assert.That(component.BoundObjective, Is.EqualTo(objective));
                Assert.That(siphon.CanActivate(beacon, secondUser), Is.False);
            });

            SEntMan.RemoveComponent<ActiveResearchDataSiphonComponent>(beacon);
            SEntMan.GetComponent<ApcPowerReceiverComponent>(beacon).Powered = false;
            Assert.That(siphon.CanActivate(beacon, firstUser), Is.False);

            SEntMan.GetComponent<ApcPowerReceiverComponent>(beacon).Powered = true;
            transform.Unanchor(beacon);
            Assert.That(siphon.CanActivate(beacon, firstUser), Is.False);

            transform.AnchorEntity(beacon);
            Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(5, 0), Tile.Empty);
            Assert.That(siphon.CanActivate(beacon, firstUser), Is.False);

            Server.System<SharedMapSystem>().SetTile(map.Grid, new Vector2i(5, 0), new Tile(1));
            SEntMan.RemoveComponent<StationMemberComponent>(map.Grid.Owner);
            Assert.That(siphon.CanActivate(beacon, firstUser), Is.False);
        });
    }

    [Test]
    public async Task PowerLossPausesElapsedTime()
    {
        var siphon = Server.System<ResearchDataSiphonSystem>();
        EntityUid beacon = default;

        await Server.WaitPost(() =>
        {
            beacon = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<ResearchDataSiphonComponent>(beacon);
            SEntMan.AddComponent<ActiveResearchDataSiphonComponent>(beacon);
            var power = SEntMan.AddComponent<ApcPowerReceiverComponent>(beacon);

            power.Powered = true;
            siphon.Update(12f);
            power.Powered = false;
            siphon.Update(50f);
            power.Powered = true;
            siphon.Update(3f);
        });

        await Server.WaitAssertion(() =>
        {
            var active = SEntMan.GetComponent<ActiveResearchDataSiphonComponent>(beacon);
            Assert.That(active.PoweredElapsed, Is.EqualTo(TimeSpan.FromSeconds(15)));
        });
    }

    [Test]
    public async Task DeactivationPreservesBindingAndStartsCooldown()
    {
        var map = await Pair.CreateTestMap();
        var siphon = Server.System<ResearchDataSiphonSystem>();
        var timing = Server.ResolveDependency<IGameTiming>();
        EntityUid beacon = default;
        EntityUid user = default;
        EntityUid mind = default;
        EntityUid objective = default;

        await Server.WaitPost(() =>
        {
            FillFloor(map.Grid);
            var station = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            var stationData = SEntMan.AddComponent<StationDataComponent>(station);
            stationData.Grids.Add(map.Grid.Owner);
            SEntMan.AddComponent<StationMemberComponent>(map.Grid.Owner).Station = station;

            user = SEntMan.SpawnEntity("MobHuman", map.GridCoords);
            mind = AttachMind(user, out var mindComponent);
            objective = SEntMan.SpawnEntity(null, MapCoordinates.Nullspace);
            SEntMan.AddComponent<StealResearchDataConditionComponent>(objective);
            mindComponent.Objectives.Add(objective);

            beacon = SEntMan.SpawnEntity("SunriseResearchDataSiphon", map.GridCoords);
            Server.System<SharedTransformSystem>().AnchorEntity(beacon);
            SEntMan.GetComponent<ApcPowerReceiverComponent>(beacon).Powered = true;
            Assert.That(siphon.TryActivate(beacon, user), Is.True);
            Assert.That(siphon.TryDeactivate(beacon, user), Is.True);

            var cancelled = new ResearchDataSiphonDeactivateDoAfterEvent();
            cancelled.DoAfter = new Content.Shared.DoAfter.DoAfter(
                0,
                new DoAfterArgs(SEntMan, user, TimeSpan.Zero, cancelled, beacon),
                timing.CurTime)
            {
                CancelledTime = timing.CurTime,
            };
            SEntMan.EventBus.RaiseLocalEvent(beacon, cancelled);
            Assert.That(SEntMan.HasComponent<ActiveResearchDataSiphonComponent>(beacon), Is.True);

            var completed = new ResearchDataSiphonDeactivateDoAfterEvent();
            completed.DoAfter = new Content.Shared.DoAfter.DoAfter(
                1,
                new DoAfterArgs(SEntMan, user, TimeSpan.Zero, completed, beacon),
                timing.CurTime);
            SEntMan.EventBus.RaiseLocalEvent(beacon, completed);
        });

        await Server.WaitAssertion(() =>
        {
            var component = SEntMan.GetComponent<ResearchDataSiphonComponent>(beacon);
            var cooldown = SEntMan.GetComponent<ResearchDataSiphonCooldownComponent>(beacon);
            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<ActiveResearchDataSiphonComponent>(beacon), Is.False);
                Assert.That(SEntMan.GetComponent<TransformComponent>(beacon).Anchored, Is.False);
                Assert.That(component.BoundMind, Is.EqualTo(mind));
                Assert.That(component.BoundObjective, Is.EqualTo(objective));
                Assert.That(cooldown.EndsAt - timing.CurTime,
                    Is.InRange(TimeSpan.FromSeconds(19), TimeSpan.FromSeconds(20)));
            });
        });
    }

    private EntityUid AttachMind(EntityUid user, out MindComponent mindComponent)
    {
        SEntMan.EnsureComponent<MindContainerComponent>(user);
        var mind = Server.System<SharedMindSystem>().CreateMind(null);
        Server.System<SharedMindSystem>().TransferTo(mind, user, mind: mind.Comp);
        mindComponent = mind.Comp;
        return mind.Owner;
    }

    private void FillFloor(Entity<MapGridComponent> grid)
    {
        var map = Server.System<SharedMapSystem>();
        for (var x = -6; x <= 6; x++)
        {
            for (var y = -6; y <= 6; y++)
            {
                map.SetTile(grid, new Vector2i(x, y), new Tile(1));
            }
        }
    }
}
