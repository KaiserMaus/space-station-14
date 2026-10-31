using System.Numerics;
using Content.Shared._Sunrise.Magic.InfiniteGuns;
using Content.Shared._Sunrise.Weapons.DualWield;
using Content.Shared.Actions;
using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Item;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Wieldable;
using Content.Shared.Wieldable.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Magic.InfiniteGuns;

public sealed partial class InfiniteGunsSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ThrowingSystem _throwing = default!;
    [Dependency] private SharedWieldableSystem _wieldable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<InfiniteGunsActionEvent>(OnInfiniteGunsAction);
        SubscribeLocalEvent<ActiveInfiniteGunsComponent, DidUnequipHandEvent>(OnHandUnequipped);
        SubscribeLocalEvent<EnchantedGunComponent, GunShotEvent>(OnEnchantedGunShot);
        SubscribeLocalEvent<EnchantedGunComponent, GotUnequippedHandEvent>(OnEnchantedGunUnequipped,
            after: [typeof(MultiHandedItemSystem)]);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<PendingEnchantedGunThrowComponent>();
        while (query.MoveNext(out var uid, out var pending))
            TryThrowAndConvert((uid, pending));
    }

    private void OnInfiniteGunsAction(InfiniteGunsActionEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartStream(args.Performer, args.WeaponPrototype, args.SpawnCount, args.BatchSize))
            args.Handled = true;
    }

    private void OnHandUnequipped(Entity<ActiveInfiniteGunsComponent> ent, ref DidUnequipHandEvent args)
    {
        TrySpawnBatch(ent.AsNullable());
    }

    private void OnEnchantedGunShot(Entity<EnchantedGunComponent> ent, ref GunShotEvent args)
    {
        TryQueueThrow(ent, args.User);
    }

    private void OnEnchantedGunUnequipped(Entity<EnchantedGunComponent> ent, ref GotUnequippedHandEvent args)
    {
        ConvertToOrdinaryEmpty(ent);
    }

    public bool TryStartStream(EntityUid user, EntProtoId weaponPrototype, int count, int batchSize)
    {
        if (!CanStartStream(user, weaponPrototype, count, batchSize))
            return false;

        DoStartStream(user, weaponPrototype, count, batchSize);
        return true;
    }

    public bool CanStartStream(EntityUid user, EntProtoId weaponPrototype, int count, int batchSize)
    {
        if (count <= 0 || batchSize <= 0)
            return false;

        if (!Exists(user) || !HasComp<HandsComponent>(user))
            return false;

        return _proto.HasIndex<EntityPrototype>(weaponPrototype);
    }

    private void DoStartStream(EntityUid user, EntProtoId weaponPrototype, int count, int batchSize)
    {
        var active = EnsureComp<ActiveInfiniteGunsComponent>(user);
        active.WeaponPrototype = weaponPrototype;
        active.RemainingCount = count;
        active.BatchSize = batchSize;

        TrySpawnBatch((user, active));
    }

    public bool TrySpawnBatch(Entity<ActiveInfiniteGunsComponent?> user)
    {
        if (!CanSpawnBatch(user))
            return false;

        DoSpawnBatch((user.Owner, user.Comp!));
        return true;
    }

    public bool CanSpawnBatch(Entity<ActiveInfiniteGunsComponent?> user)
    {
        if (!Resolve(user, ref user.Comp, false))
            return false;

        if (LifeStage(user) >= EntityLifeStage.Terminating)
            return false;

        if (user.Comp.Spawning || user.Comp.RemainingCount <= 0 || user.Comp.BatchSize <= 0)
            return false;

        if (!TryComp<HandsComponent>(user, out var hands))
            return false;

        if (_hands.CountFreeHands((user.Owner, hands)) < 2)
            return false;

        foreach (var held in _hands.EnumerateHeld((user.Owner, hands)))
        {
            if (!HasComp<VirtualItemComponent>(held))
                return false;
        }

        return true;
    }

    private void DoSpawnBatch(Entity<ActiveInfiniteGunsComponent> user)
    {
        var spawnCount = Math.Min(user.Comp.BatchSize, user.Comp.RemainingCount);
        user.Comp.Spawning = true;
        try
        {
            for (var i = 0; i < spawnCount; i++)
            {
                var weapon = Spawn(user.Comp.WeaponPrototype, Transform(user).Coordinates);
                if (!TryComp<BallisticAmmoProviderComponent>(weapon, out var ammo))
                {
                    Del(weapon);
                    continue;
                }

                _gun.SetBallisticUnspawned((weapon, ammo), 1);
                if (!_hands.TryPickupAnyHand(user, weapon, animate: false))
                {
                    Del(weapon);
                    continue;
                }

                if (TryComp<WieldableComponent>(weapon, out var wieldable) &&
                    !_wieldable.TryWield((weapon, wieldable), user, silent: true))
                {
                    _hands.TryDrop(user.Owner, entity: weapon, checkActionBlocker: false, doDropInteraction: false);
                    Del(weapon);
                    continue;
                }

                user.Comp.RemainingCount--;
            }
        }
        finally
        {
            user.Comp.Spawning = false;
        }

        if (user.Comp.RemainingCount <= 0)
            RemComp<ActiveInfiniteGunsComponent>(user);
    }

    public bool TryQueueThrow(Entity<EnchantedGunComponent> gun, EntityUid user)
    {
        if (HasComp<PendingEnchantedGunThrowComponent>(gun))
            return false;

        var pending = AddComp<PendingEnchantedGunThrowComponent>(gun);
        pending.User = user;
        return true;
    }

    private bool TryThrowAndConvert(Entity<PendingEnchantedGunThrowComponent> gun)
    {
        if (!TryComp<EnchantedGunComponent>(gun, out var enchanted))
        {
            RemComp<PendingEnchantedGunThrowComponent>(gun);
            return false;
        }

        if (!_hands.IsHolding(gun.Comp.User, gun, out _))
        {
            ConvertToOrdinaryEmpty((gun.Owner, enchanted));
            return false;
        }

        if (!_hands.TryDrop(gun.Comp.User, gun, checkActionBlocker: false, doDropInteraction: false))
        {
            RemComp<PendingEnchantedGunThrowComponent>(gun);
            return false;
        }

        var direction = _random.NextVector2();
        if (direction.LengthSquared() < 0.001f)
            direction = Vector2.UnitX;

        return _throwing.TryThrow(gun, direction * 2f, baseThrowSpeed: 4f, user: gun.Comp.User,
            compensateFriction: true);
    }

    private void ConvertToOrdinaryEmpty(Entity<EnchantedGunComponent> gun)
    {
        if (TryComp<BallisticAmmoProviderComponent>(gun, out var ammo))
            _gun.EmptyBallisticAmmo((gun.Owner, ammo));

        if (TryComp<GunComponent>(gun, out var gunComponent))
            _gun.SetResetOnHandSelected((gun.Owner, gunComponent), true);

        if (TryComp<MeleeWeaponComponent>(gun, out var melee))
        {
            melee.ResetOnHandSelected = true;
            Dirty(gun, melee);
        }

        var ordinary = _proto.Index<EntityPrototype>(gun.Comp.OrdinaryPrototype);
        _metadata.SetEntityName(gun, ordinary.Name);
        _metadata.SetEntityDescription(gun, ordinary.Description);

        RemComp<PendingEnchantedGunThrowComponent>(gun);
        RemComp<MultiHandedItemComponent>(gun);
        RemComp<CanDualWieldComponent>(gun);
        RemComp<EnchantedGunComponent>(gun);
    }
}
