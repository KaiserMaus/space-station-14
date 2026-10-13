using System.Collections.Generic;
using System.Linq;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._Sunrise.Weapons.Ranged;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.Weapons;

public sealed class BeltFeedTests : InteractionTest
{
    private static readonly EntProtoId Rpd = "WeaponLightMachineGunRPD";

    [TestCaseSource(nameof(BeltFedMachineGuns))]
    public async Task BeltFedMachineGunsLockMagazineWithClosedCoverTest(EntProtoId prototype)
    {
        var gun = ToServer(await SpawnTarget(prototype));
        var chamber = SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(gun);
        var magazineSlot = SEntMan.GetComponent<ItemSlotsComponent>(gun).Slots["gun_magazine"];

        Assert.Multiple(() =>
        {
            Assert.That(SEntMan.HasComponent<BeltFeedComponent>(gun), Is.True);
            Assert.That(chamber.BoltOpenOnEmpty, Is.False);
            Assert.That(magazineSlot.Swap, Is.False);
            Assert.That(magazineSlot.EjectOnInteract, Is.False);
        });

        await Server.WaitPost(() => SGun.SetBoltClosed(gun, chamber, true, SPlayer));

        Assert.That(magazineSlot.Locked, Is.True);
    }

    private static IEnumerable<EntProtoId> BeltFedMachineGuns()
    {
        yield return "WeaponLightMachineGunL6";
        yield return "WeaponLightMachineGunL6Max";
        yield return "WeaponLightMachineGunL6Fake";
        yield return "WeaponLightMachineGunMG60";
        yield return "WeaponLightMachineGunMG60Empty";
        yield return Rpd;
        yield return "WeaponMachineGunMG42";
        yield return "WeaponMachineGunLCG";
        yield return "WeaponMachineGunLCGEmpty";
        yield return "WeaponLightMachineGunPirateCannon";
    }

    [Test]
    public async Task RpdMagazineSlotRequiresAlternativeRemovalTest()
    {
        var rpd = ToServer(await SpawnTarget(Rpd));
        var slots = SEntMan.GetComponent<ItemSlotsComponent>(rpd);
        var magazineSlot = slots.Slots["gun_magazine"];

        Assert.Multiple(() =>
        {
            Assert.That(magazineSlot.Swap, Is.False);
            Assert.That(magazineSlot.EjectOnInteract, Is.False);
            Assert.That(magazineSlot.Locked, Is.False);
        });
    }

    [Test]
    public async Task RpdUsesBeltFeedComponentTest()
    {
        var rpd = ToServer(await SpawnTarget(Rpd));

        Assert.That(Factory.TryGetRegistration("BeltFeed", out var registration), Is.True);
        Assert.That(registration, Is.Not.Null);
        Assert.That(SEntMan.HasComponent(rpd, registration!), Is.True);
    }

    [Test]
    public async Task OpeningBoltAllowsMagazineRemovalTest()
    {
        var rpd = ToServer(await SpawnTarget(Rpd));
        var chamber = SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(rpd);
        var slots = SEntMan.GetComponent<ItemSlotsComponent>(rpd);
        var magazineSlot = slots.Slots["gun_magazine"];

        await Server.WaitPost(() =>
        {
            SGun.SetBoltClosed(rpd, chamber, true, SPlayer);
            SGun.SetBoltClosed(rpd, chamber, false, SPlayer);
        });

        Assert.That(magazineSlot.Locked, Is.False);

        var removed = false;
        await Server.WaitPost(() => removed = SEntMan.System<ItemSlotsSystem>()
            .TryEjectToHands(rpd, magazineSlot, SPlayer));

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(magazineSlot.Item, Is.Null);
        });
    }

    [Test]
    public async Task OpenBoltProvidesMagazineRemovalVerbTest()
    {
        var rpd = ToServer(await SpawnTarget(Rpd));
        var chamber = SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(rpd);
        var slots = SEntMan.GetComponent<ItemSlotsComponent>(rpd);
        var magazineSlot = slots.Slots["gun_magazine"];

        await Server.WaitPost(() =>
        {
            SGun.SetBoltClosed(rpd, chamber, true, SPlayer);
            SGun.SetBoltClosed(rpd, chamber, false, SPlayer);
        });

        var magazine = SEntMan.GetNetEntity(magazineSlot.Item);
        AlternativeVerb magazineVerb = null!;

        await Server.WaitPost(() =>
        {
            var verbs = SEntMan.System<SharedVerbSystem>()
                .GetLocalVerbs(rpd, SPlayer, typeof(AlternativeVerb), force: true);
            magazineVerb = verbs.OfType<AlternativeVerb>()
                .SingleOrDefault(verb => verb.IconEntity == magazine);
        });

        Assert.That(magazineVerb, Is.Not.Null);

        await Server.WaitPost(() => magazineVerb!.Act!());

        Assert.That(magazineSlot.Item, Is.Null);
    }

    [Test]
    public async Task ClosingBoltRequiresCycleBeforeFiringTest()
    {
        var rpd = ToServer(await SpawnTarget(Rpd));
        var chamber = SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(rpd);

        await Server.WaitPost(() =>
        {
            SGun.SetBoltClosed(rpd, chamber, true, SPlayer);
            SGun.SetBoltClosed(rpd, chamber, false, SPlayer);
            SGun.SetBoltClosed(rpd, chamber, true, SPlayer);
        });

        var component = SEntMan.GetComponent<BeltFeedComponent>(rpd);
        Assert.That(component.RequiresCycle, Is.True);

        var attempt = new AttemptShootEvent(SPlayer, null);
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(rpd, ref attempt));

        Assert.That(attempt.Cancelled, Is.True);
    }

    [Test]
    public async Task ClosingBoltDoesNotChamberRoundUntilRackedTest()
    {
        var rpd = ToServer(await SpawnTarget(Rpd));
        var beltFeed = SEntMan.System<BeltFeedSystem>();
        var chamber = SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(rpd);

        await Server.WaitPost(() => SGun.SetBoltClosed(rpd, chamber, true, SPlayer));

        Assert.That(SGun.GetChamberEntity(rpd), Is.Null);

        var cycled = false;
        await Server.WaitPost(() => cycled = beltFeed.TryCycle(rpd, SPlayer));

        Assert.Multiple(() =>
        {
            Assert.That(cycled, Is.True);
            Assert.That(SGun.GetChamberEntity(rpd), Is.Not.Null);
            Assert.That(SEntMan.GetComponent<BeltFeedComponent>(rpd).RequiresCycle, Is.False);
        });
    }

    [Test]
    public async Task OpeningBoltKeepsChamberedRoundTest()
    {
        var rpd = ToServer(await SpawnTarget(Rpd));
        var beltFeed = SEntMan.System<BeltFeedSystem>();
        var chamber = SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(rpd);
        var chamberSlot = SEntMan.GetComponent<ItemSlotsComponent>(rpd).Slots["gun_chamber"];

        await Server.WaitPost(() =>
        {
            SGun.SetBoltClosed(rpd, chamber, true, SPlayer);
            beltFeed.TryCycle(rpd, SPlayer);
        });

        var chambered = SGun.GetChamberEntity(rpd);
        Assert.That(chambered, Is.Not.Null);

        await Server.WaitPost(() => SGun.SetBoltClosed(rpd, chamber, false, SPlayer));

        Assert.Multiple(() =>
        {
            Assert.That(SGun.GetChamberEntity(rpd), Is.EqualTo(chambered));
            Assert.That(chamber.BoltClosed, Is.False);
            Assert.That(chamberSlot.Locked, Is.True);
            Assert.That(SEntMan.GetComponent<BeltFeedComponent>(rpd).RequiresCycle, Is.False);
        });
    }

    [Test]
    public async Task OpenBoltCycleRequiresRemovedMagazineTest()
    {
        var rpd = ToServer(await SpawnTarget(Rpd));
        var beltFeed = SEntMan.System<BeltFeedSystem>();
        var itemSlots = SEntMan.System<ItemSlotsSystem>();
        var slots = SEntMan.GetComponent<ItemSlotsComponent>(rpd);
        var magazineSlot = slots.Slots["gun_magazine"];
        var chamber = SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(rpd);

        await Server.WaitPost(() =>
        {
            SGun.SetBoltClosed(rpd, chamber, true, SPlayer);
            beltFeed.TryCycle(rpd, SPlayer);
            SGun.SetBoltClosed(rpd, chamber, false, SPlayer);
        });

        var cycledWithMagazine = true;
        await Server.WaitPost(() => cycledWithMagazine = beltFeed.TryCycle(rpd, SPlayer));

        Assert.Multiple(() =>
        {
            Assert.That(cycledWithMagazine, Is.False);
            Assert.That(SGun.GetChamberEntity(rpd), Is.Not.Null);
        });

        var removed = false;
        await Server.WaitPost(() => removed = itemSlots.TryEjectToHands(rpd, magazineSlot, SPlayer));
        Assert.That(removed, Is.True);

        var cycledWithoutMagazine = false;
        await Server.WaitPost(() => cycledWithoutMagazine = beltFeed.TryCycle(rpd, SPlayer));

        Assert.Multiple(() =>
        {
            Assert.That(cycledWithoutMagazine, Is.True);
            Assert.That(SGun.GetChamberEntity(rpd), Is.Null);
            Assert.That(chamber.BoltClosed, Is.False);
            Assert.That(SEntMan.GetComponent<BeltFeedComponent>(rpd).RequiresCycle, Is.True);
        });
    }

    [Test]
    public async Task TacticalReloadKeepsChamberedRoundReadyTest()
    {
        var rpd = ToServer(await SpawnTarget(Rpd));
        var beltFeed = SEntMan.System<BeltFeedSystem>();
        var itemSlots = SEntMan.System<ItemSlotsSystem>();
        var slots = SEntMan.GetComponent<ItemSlotsComponent>(rpd);
        var magazineSlot = slots.Slots["gun_magazine"];
        var chamber = SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(rpd);

        await Server.WaitPost(() =>
        {
            SGun.SetBoltClosed(rpd, chamber, true, SPlayer);
            beltFeed.TryCycle(rpd, SPlayer);
            SGun.SetBoltClosed(rpd, chamber, false, SPlayer);
        });

        var chambered = SGun.GetChamberEntity(rpd);
        Assert.That(chambered, Is.Not.Null);

        var removed = false;
        await Server.WaitPost(() => removed = itemSlots.TryEjectToHands(rpd, magazineSlot, SPlayer));
        Assert.That(removed, Is.True);

        await DeleteHeldEntity();
        await PlaceInHands("MagazineLightRifleBeltBox");

        var inserted = false;
        await Server.WaitPost(() => inserted = itemSlots.TryInsertFromHand(rpd, magazineSlot, SPlayer));
        Assert.That(inserted, Is.True);

        await Server.WaitPost(() => SGun.SetBoltClosed(rpd, chamber, true, SPlayer));

        var attempt = new AttemptShootEvent(SPlayer, null);
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(rpd, ref attempt));

        Assert.Multiple(() =>
        {
            Assert.That(SGun.GetChamberEntity(rpd), Is.EqualTo(chambered));
            Assert.That(SEntMan.GetComponent<BeltFeedComponent>(rpd).RequiresCycle, Is.False);
            Assert.That(attempt.Cancelled, Is.False);
        });
    }

    [Test]
    public async Task RpdKeepsBoltClosedWhenMagazineBecomesEmptyTest()
    {
        var rpd = ToServer(await SpawnTarget(Rpd));
        var beltFeed = SEntMan.System<BeltFeedSystem>();
        var chamber = SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(rpd);

        await Server.WaitPost(() =>
        {
            SGun.SetBoltClosed(rpd, chamber, true, SPlayer);
            beltFeed.TryCycle(rpd, SPlayer);
        });

        var ammoCount = new GetAmmoCountEvent();
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(rpd, ref ammoCount));

        for (var i = 0; i < ammoCount.Count; i++)
        {
            var takeAmmo = new TakeAmmoEvent(1, [], SEntMan.GetCoordinates(TargetCoords), SPlayer);
            await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(rpd, takeAmmo));
            Assert.That(takeAmmo.Ammo, Has.Count.EqualTo(1));
        }

        ammoCount = new GetAmmoCountEvent();
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(rpd, ref ammoCount));

        Assert.Multiple(() =>
        {
            Assert.That(ammoCount.Count, Is.Zero);
            Assert.That(chamber.BoltClosed, Is.True);
        });
    }

    [Test]
    public async Task RpdRequiresCompleteManualReloadSequenceTest()
    {
        var rpd = ToServer(await SpawnTarget(Rpd));
        var beltFeed = SEntMan.System<BeltFeedSystem>();
        var itemSlots = SEntMan.System<ItemSlotsSystem>();
        var slots = SEntMan.GetComponent<ItemSlotsComponent>(rpd);
        var magazineSlot = slots.Slots["gun_magazine"];
        var chamber = SEntMan.GetComponent<ChamberMagazineAmmoProviderComponent>(rpd);
        await PlaceInHands("MagazineLightRifleBeltBox");

        var insertedWhileClosed = false;
        await Server.WaitPost(() => insertedWhileClosed = itemSlots.TryInsertFromHand(
            rpd,
            magazineSlot,
            SPlayer));

        Assert.That(insertedWhileClosed, Is.False);

        await DeleteHeldEntity();

        var removed = false;
        await Server.WaitPost(() => removed = itemSlots.TryEjectToHands(rpd, magazineSlot, SPlayer));

        Assert.Multiple(() =>
        {
            Assert.That(removed, Is.True);
            Assert.That(magazineSlot.Item, Is.Null);
        });

        await DeleteHeldEntity();
        await PlaceInHands("MagazineLightRifleBeltBox");

        var inserted = false;
        await Server.WaitPost(() => inserted = itemSlots.TryInsertFromHand(
            rpd,
            magazineSlot,
            SPlayer));

        Assert.That(inserted, Is.True);

        await Server.WaitPost(() => SGun.SetBoltClosed(rpd, chamber, true, SPlayer));

        var attemptBeforeCycle = new AttemptShootEvent(SPlayer, null);
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(rpd, ref attemptBeforeCycle));

        Assert.That(attemptBeforeCycle.Cancelled, Is.True);

        var cycled = false;
        await Server.WaitPost(() => cycled = beltFeed.TryCycle(rpd, SPlayer));

        var attemptAfterCycle = new AttemptShootEvent(SPlayer, null);
        await Server.WaitPost(() => SEntMan.EventBus.RaiseLocalEvent(rpd, ref attemptAfterCycle));

        Assert.Multiple(() =>
        {
            Assert.That(cycled, Is.True);
            Assert.That(attemptAfterCycle.Cancelled, Is.False);
            Assert.That(magazineSlot.Locked, Is.True);
        });
    }
}
