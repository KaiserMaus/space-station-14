using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures;
using Content.Server._Sunrise.Magic.InfiniteGuns;
using Content.Shared._Sunrise.Magic.InfiniteGuns;
using Content.Shared._Sunrise.Weapons.DualWield;
using Content.Shared.Administration;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.Magic;

[TestFixture]
[TestOf(typeof(InfiniteGunsSystem))]
public sealed class InfiniteGunsSystemTest : GameTest
{
    public override PoolSettings PoolSettings => PsDisconnected;

    private InfiniteGunsSystem _infiniteGuns = default!;
    private SharedHandsSystem _hands = default!;
    private SharedGunSystem _gun = default!;
    private IPrototypeManager _prototypes = default!;

    [SetUp]
    public async Task Setup()
    {
        await Server.WaitPost(() =>
        {
            _infiniteGuns = SEntMan.System<InfiniteGunsSystem>();
            _hands = SEntMan.System<SharedHandsSystem>();
            _gun = SEntMan.System<SharedGunSystem>();
            _prototypes = Server.ResolveDependency<IPrototypeManager>();
        });
    }

    [Test]
    public async Task LoadedGunRejectsManualDropAndEmptyGunConvertsOnDrop()
    {
        await Server.WaitAssertion(() =>
        {
            var mapSystem = SEntMan.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            var map = mapSystem.GetMap(mapId);
            var user = SSpawnAtPosition("MobHuman", new EntityCoordinates(map, 0, 0));
            var gun = SSpawnAtPosition("EnchantedWeaponPistolFlintlock", new EntityCoordinates(map, 0, 0));

            try
            {
                Assert.That(_hands.TryPickupAnyHand(user, gun, animate: false), Is.True);
                _gun.SetBallisticUnspawned((gun, SComp<BallisticAmmoProviderComponent>(gun)), 1);
                Assert.That(_hands.TryDrop(user, entity: gun), Is.False);
                var loadedThrow = new BeforeThrowEvent(gun, Vector2.UnitX, 4f, user);
                SEntMan.EventBus.RaiseLocalEvent(user, ref loadedThrow);
                Assert.That(loadedThrow.Cancelled, Is.True);

                _gun.SetBallisticUnspawned((gun, SComp<BallisticAmmoProviderComponent>(gun)), 0);
                var emptyThrow = new BeforeThrowEvent(gun, Vector2.UnitX, 4f, user);
                SEntMan.EventBus.RaiseLocalEvent(user, ref emptyThrow);
                Assert.That(emptyThrow.Cancelled, Is.False);
                Assert.That(_hands.TryDrop(user, entity: gun), Is.True);
                AssertOrdinaryEmptyGun(gun, "WeaponPistolFlintlock");
            }
            finally
            {
                SEntMan.DeleteEntity(map);
            }
        });
    }

    [TestCase("EnchantedWeaponSniperMosin")]
    [TestCase("EnchantedMusket")]
    public async Task SpawnedLongGunIsWieldedAndCanShoot(string prototype)
    {
        await Server.WaitAssertion(() =>
        {
            var user = SSpawn("MobHuman");
            Assert.That(_infiniteGuns.TryStartStream(user, prototype, 1, 1), Is.True);
            var gun = GetRealHeldItems(user).Single();

            var attempted = new ShotAttemptedEvent
            {
                User = user,
                Used = (gun, SComp<GunComponent>(gun)),
            };
            SEntMan.EventBus.RaiseLocalEvent(gun, ref attempted);

            Assert.Multiple(() =>
            {
                Assert.That(SEntMan.HasComponent<MultiHandedItemComponent>(gun), Is.False);
                Assert.That(SComp<WieldableComponent>(gun).Wielded, Is.True);
                Assert.That(attempted.Cancelled, Is.False);
            });
        });
    }

    [Test]
    public async Task ShotDefersThrowConvertsGunAndSpawnsNextBatch()
    {
        EntityUid map = default;
        EntityUid user = default;
        EntityUid firedGun = default;

        try
        {
            await Server.WaitAssertion(() =>
            {
                var mapSystem = SEntMan.System<SharedMapSystem>();
                mapSystem.CreateMap(out var mapId);
                map = mapSystem.GetMap(mapId);
                user = SSpawnAtPosition("MobHuman", new EntityCoordinates(map, 0, 0));

                Assert.That(_infiniteGuns.TryStartStream(user, "EnchantedWeaponSniperMosin", 2, 1), Is.True);
                firedGun = GetRealHeldItems(user).Single();

                var shot = new GunShotEvent(user, new List<(EntityUid?, IShootable)>());
                SEntMan.EventBus.RaiseLocalEvent(firedGun, ref shot);

                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<PendingEnchantedGunThrowComponent>(firedGun), Is.True);
                    Assert.That(GetRealHeldItems(user), Is.EqualTo(new[] { firedGun }));
                });
            });

            await Server.WaitRunTicks(1);

            await Server.WaitAssertion(() =>
            {
                AssertOrdinaryEmptyGun(firedGun, "WeaponSniperMosin");
                Assert.Multiple(() =>
                {
                    Assert.That(SEntMan.HasComponent<ThrownItemComponent>(firedGun), Is.True);
                    Assert.That(SComp<PhysicsComponent>(firedGun).LinearVelocity.LengthSquared(), Is.GreaterThan(0));
                    Assert.That(GetRealHeldItems(user), Has.Count.EqualTo(1));
                    Assert.That(GetRealHeldItems(user)[0], Is.Not.EqualTo(firedGun));
                    Assert.That(SEntMan.HasComponent<ActiveInfiniteGunsComponent>(user), Is.False);
                });
            });
        }
        finally
        {
            await Server.WaitPost(() =>
            {
                if (SEntMan.EntityExists(map))
                    SEntMan.DeleteEntity(map);
            });
        }
    }

    [Test]
    public async Task ForcedLoadedUnequipConvertsGunToEmpty()
    {
        await Server.WaitAssertion(() =>
        {
            var mapSystem = SEntMan.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            var map = mapSystem.GetMap(mapId);
            var user = SSpawnAtPosition("MobHuman", new EntityCoordinates(map, 0, 0));
            var gun = SSpawnAtPosition("EnchantedWeaponPistolFlintlock", new EntityCoordinates(map, 0, 0));

            try
            {
                Assert.That(_hands.TryPickupAnyHand(user, gun, animate: false), Is.True);
                _gun.SetBallisticUnspawned((gun, SComp<BallisticAmmoProviderComponent>(gun)), 1);
                Assert.That(_hands.IsHolding(user, gun, out var hand), Is.True);

                _hands.DoDrop(user, hand, doDropInteraction: false);

                AssertOrdinaryEmptyGun(gun, "WeaponPistolFlintlock");
            }
            finally
            {
                SEntMan.DeleteEntity(map);
            }
        });
    }

    [Test]
    public async Task ConvertedGunClearsContainedAmmoReferences()
    {
        await Server.WaitAssertion(() =>
        {
            var mapSystem = SEntMan.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            var map = mapSystem.GetMap(mapId);
            var coordinates = new EntityCoordinates(map, 0, 0);
            var user = SSpawnAtPosition("MobHuman", coordinates);

            try
            {
                Assert.That(_infiniteGuns.TryStartStream(user, "EnchantedWeaponSniperMosin", 1, 1), Is.True);
                var gun = GetRealHeldItems(user).Single();
                var ammo = SComp<BallisticAmmoProviderComponent>(gun);
                Assert.That(ammo.Proto, Is.Not.Null);

                _gun.SetBallisticUnspawned((gun, ammo), 0);
                var cartridge = SEntMan.SpawnEntity(ammo.Proto!.Value, coordinates);
                Assert.That(_gun.TryBallisticInsert((gun, ammo), cartridge, user), Is.True);
                Assert.That(ammo.Entities, Has.Count.EqualTo(1));
                Assert.That(_hands.IsHolding(user, gun, out var hand), Is.True);

                _hands.DoDrop(user, hand, doDropInteraction: false);

                Assert.Multiple(() =>
                {
                    Assert.That(ammo.Entities, Is.Empty);
                    Assert.That(ammo.Container.ContainedEntities, Is.Empty);
                    Assert.That(_gun.GetAmmoCount(gun), Is.Zero);
                });
            }
            finally
            {
                SEntMan.DeleteEntity(map);
            }
        });
    }

    [Test]
    public async Task StartStreamResetsAllowanceWithoutAccumulating()
    {
        await Server.WaitAssertion(() =>
        {
            var user = SSpawn("MobHuman");
            SEntMan.System<AdminFrozenSystem>().FreezeAndMute(user);

            Assert.That(_infiniteGuns.TryStartStream(user, "EnchantedWeaponSniperMosin", 31, 1), Is.True);
            var active = SComp<ActiveInfiniteGunsComponent>(user);
            Assert.That(active.RemainingCount, Is.EqualTo(31));

            active.RemainingCount = 4;
            Assert.That(_infiniteGuns.TryStartStream(user, "EnchantedWeaponSniperMosin", 31, 1), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(active.RemainingCount, Is.EqualTo(31));
                Assert.That(active.WeaponPrototype, Is.EqualTo((EntProtoId) "EnchantedWeaponSniperMosin"));
                Assert.That(active.BatchSize, Is.EqualTo(1));
            });
        });
    }

    [Test]
    public async Task FailedPickupDoesNotConsumeAllowanceOrLeakGun()
    {
        await Server.WaitAssertion(() =>
        {
            var user = SSpawn("MobHuman");
            SEntMan.System<AdminFrozenSystem>().FreezeAndMute(user);
            var before = SEntMan.EntityQuery<EnchantedGunComponent>().Count();

            Assert.That(_infiniteGuns.TryStartStream(user, "EnchantedWeaponSniperMosin", 31, 1), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(SComp<ActiveInfiniteGunsComponent>(user).RemainingCount, Is.EqualTo(31));
                Assert.That(SEntMan.EntityQuery<EnchantedGunComponent>().Count(), Is.EqualTo(before));
                Assert.That(GetRealHeldItems(user), Is.Empty);
            });
        });
    }

    [Test]
    public async Task OccupiedHandBlocksEntireBatch()
    {
        await Server.WaitAssertion(() =>
        {
            var user = SSpawn("MobHuman");
            var item = SSpawn("Crowbar");
            Assert.That(_hands.TryPickupAnyHand(user, item, animate: false), Is.True);

            Assert.That(_infiniteGuns.TryStartStream(user, "EnchantedWeaponPistolFlintlock", 31, 2), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(SComp<ActiveInfiniteGunsComponent>(user).RemainingCount, Is.EqualTo(31));
                Assert.That(GetRealHeldItems(user), Is.EqualTo(new[] { item }));
            });
        });
    }

    [Test]
    public async Task LongGunSpawnsLoadedAndConsumesOneItem()
    {
        await Server.WaitAssertion(() =>
        {
            var user = SSpawn("MobHuman");

            Assert.That(_infiniteGuns.TryStartStream(user, "EnchantedWeaponSniperMosin", 31, 1), Is.True);

            var held = GetRealHeldItems(user);
            Assert.Multiple(() =>
            {
                Assert.That(held, Has.Count.EqualTo(1));
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(held[0]).EntityPrototype?.ID,
                    Is.EqualTo("EnchantedWeaponSniperMosin"));
                Assert.That(_gun.GetAmmoCount(held[0]), Is.EqualTo(1));
                Assert.That(SComp<ActiveInfiniteGunsComponent>(user).RemainingCount, Is.EqualTo(30));
            });
        });
    }

    [Test]
    public async Task PistolBoundarySpawnsPairThenFinalSingle()
    {
        await Server.WaitAssertion(() =>
        {
            var mapSystem = SEntMan.System<SharedMapSystem>();
            mapSystem.CreateMap(out var mapId);
            var map = mapSystem.GetMap(mapId);
            var user = SSpawnAtPosition("MobHuman", new EntityCoordinates(map, 0, 0));

            try
            {
                Assert.That(_infiniteGuns.TryStartStream(user, "EnchantedWeaponPistolFlintlock", 3, 2), Is.True);

                var held = GetRealHeldItems(user);
                Assert.Multiple(() =>
                {
                    Assert.That(held, Has.Count.EqualTo(2));
                    Assert.That(SEntMan.HasComponent<DualWieldComponent>(user), Is.True);
                    Assert.That(SComp<ActiveInfiniteGunsComponent>(user).RemainingCount, Is.EqualTo(1));
                });

                foreach (var weapon in held)
                    _gun.SetBallisticUnspawned((weapon, SComp<BallisticAmmoProviderComponent>(weapon)), 0);

                Assert.That(_hands.TryDrop(user, entity: held[0], checkActionBlocker: false), Is.True);
                Assert.That(_hands.TryDrop(user, entity: held[1], checkActionBlocker: false), Is.True);

                var finalHeld = GetRealHeldItems(user);
                Assert.Multiple(() =>
                {
                    Assert.That(finalHeld, Has.Count.EqualTo(1));
                    Assert.That(SEntMan.GetComponent<MetaDataComponent>(finalHeld[0]).EntityPrototype?.ID,
                        Is.EqualTo("EnchantedWeaponPistolFlintlock"));
                    Assert.That(SEntMan.HasComponent<ActiveInfiniteGunsComponent>(user), Is.False);
                });
            }
            finally
            {
                SEntMan.DeleteEntity(map);
            }
        });
    }

    private List<EntityUid> GetRealHeldItems(EntityUid user)
    {
        var result = new List<EntityUid>();
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (SEntMan.HasComponent<VirtualItemComponent>(held))
                continue;

            result.Add(held);
        }

        return result;
    }

    private void AssertOrdinaryEmptyGun(EntityUid gun, EntProtoId ordinaryPrototype)
    {
        var prototype = _prototypes.Index<EntityPrototype>(ordinaryPrototype);
        Assert.Multiple(() =>
        {
            Assert.That(_gun.GetAmmoCount(gun), Is.Zero);
            Assert.That(SEntMan.HasComponent<EnchantedGunComponent>(gun), Is.False);
            Assert.That(SEntMan.HasComponent<PendingEnchantedGunThrowComponent>(gun), Is.False);
            Assert.That(SEntMan.HasComponent<MultiHandedItemComponent>(gun), Is.False);
            Assert.That(SEntMan.HasComponent<CanDualWieldComponent>(gun), Is.False);
            Assert.That(SComp<GunComponent>(gun).ResetOnHandSelected, Is.True);
            Assert.That(SEntMan.TryGetComponent<MeleeWeaponComponent>(gun, out var melee) && !melee.ResetOnHandSelected,
                Is.False);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(gun).EntityName, Is.EqualTo(prototype.Name));
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(gun).EntityDescription, Is.EqualTo(prototype.Description));
        });
    }
}
