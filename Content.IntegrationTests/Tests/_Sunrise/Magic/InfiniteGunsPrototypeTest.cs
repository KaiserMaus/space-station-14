using Content.IntegrationTests.Fixtures;
using Content.Shared._Sunrise.Magic.InfiniteGuns;
using Content.Shared._Sunrise.Weapons.DualWield;
using Content.Shared.Actions.Components;
using Content.Shared.Item;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Sunrise.Magic;

[TestFixture]
[TestOf(typeof(InfiniteGunsActionEvent))]
[TestOf(typeof(EnchantedGunComponent))]
public sealed class InfiniteGunsPrototypeTest : GameTest
{
    private const string MosinAction = "ActionInfiniteGuns";
    private const string FlintlockAction = "ActionInfiniteGunsII";
    private const string MusketAction = "ActionInfiniteGunsIII";

    [Test]
    public async Task ActionsConfigureThreeBoundedLevels()
    {
        await Server.WaitAssertion(() =>
        {
            AssertAction(MosinAction, 1, "EnchantedWeaponSniperMosin", 1);
            AssertAction(FlintlockAction, 2, "EnchantedWeaponPistolFlintlock", 2);
            AssertAction(MusketAction, 3, "EnchantedMusket", 1);

            var baseAction = SEntMan.SpawnEntity(MosinAction, MapCoordinates.Nullspace);
            var upgrade = SEntMan.GetComponent<ActionUpgradeComponent>(baseAction);

            Assert.Multiple(() =>
            {
                Assert.That(upgrade.EffectedLevels[2], Is.EqualTo((EntProtoId) FlintlockAction));
                Assert.That(upgrade.EffectedLevels[3], Is.EqualTo((EntProtoId) MusketAction));
            });
        });
    }

    [TestCase("EnchantedWeaponSniperMosin", "WeaponSniperMosin", false, false)]
    [TestCase("EnchantedWeaponPistolFlintlock", "WeaponPistolFlintlock", false, true)]
    [TestCase("EnchantedMusket", "Musket", false, false)]
    public async Task EnchantedWeaponsKeepCapacityAndConfigureHands(
        string enchantedPrototype,
        string ordinaryPrototype,
        bool multiHanded,
        bool dualWield)
    {
        await Server.WaitAssertion(() =>
        {
            var prototype = SProtoMan.Index<EntityPrototype>(enchantedPrototype);
            var enchanted = SEntMan.SpawnEntity(enchantedPrototype, MapCoordinates.Nullspace);
            var ordinary = SEntMan.SpawnEntity(ordinaryPrototype, MapCoordinates.Nullspace);

            var enchantedAmmo = SEntMan.GetComponent<BallisticAmmoProviderComponent>(enchanted);
            var ordinaryAmmo = SEntMan.GetComponent<BallisticAmmoProviderComponent>(ordinary);
            var gun = SEntMan.GetComponent<GunComponent>(enchanted);
            var marker = SEntMan.GetComponent<EnchantedGunComponent>(enchanted);

            Assert.Multiple(() =>
            {
                Assert.That(prototype.Parents, Does.Contain(ordinaryPrototype));
                Assert.That(marker.OrdinaryPrototype, Is.EqualTo((EntProtoId) ordinaryPrototype));
                Assert.That(enchantedAmmo.Capacity, Is.EqualTo(ordinaryAmmo.Capacity));
                Assert.That(gun.ResetOnHandSelected, Is.False);
                Assert.That(SEntMan.HasComponent<MultiHandedItemComponent>(enchanted), Is.EqualTo(multiHanded));
                Assert.That(SEntMan.HasComponent<CanDualWieldComponent>(enchanted), Is.EqualTo(dualWield));
            });

            if (SEntMan.TryGetComponent<MeleeWeaponComponent>(enchanted, out var melee))
                Assert.That(melee.ResetOnHandSelected, Is.False);

            if (SEntMan.TryGetComponent<CanDualWieldComponent>(enchanted, out var canDualWield))
                Assert.That(canDualWield.DualWieldFireRatePenalty, Is.Zero);
        });
    }

    private void AssertAction(string prototype, int level, EntProtoId weapon, int batchSize)
    {
        var action = SEntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
        var actionComponent = SEntMan.GetComponent<ActionComponent>(action);
        var instantAction = SEntMan.GetComponent<InstantActionComponent>(action);
        var upgrade = SEntMan.GetComponent<ActionUpgradeComponent>(action);

        Assert.That(instantAction.Event, Is.TypeOf<InfiniteGunsActionEvent>());
        var spellEvent = (InfiniteGunsActionEvent) instantAction.Event!;

        Assert.Multiple(() =>
        {
            Assert.That(actionComponent.UseDelay, Is.EqualTo(TimeSpan.FromSeconds(60)));
            Assert.That(upgrade.Level, Is.EqualTo(level));
            Assert.That(spellEvent.WeaponPrototype, Is.EqualTo(weapon));
            Assert.That(spellEvent.SpawnCount, Is.EqualTo(31));
            Assert.That(spellEvent.BatchSize, Is.EqualTo(batchSize));
        });
    }
}
