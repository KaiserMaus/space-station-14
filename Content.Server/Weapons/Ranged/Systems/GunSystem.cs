using System.Linq;
using System.Numerics;
using Content.Server.Cargo.Systems;
using Content.Server.Hands.Systems;
using Content.Server.Power.EntitySystems;
using Content.Server.Weapons.Ranged.Components;
using Content.Shared.Cargo;
using Content.Shared._Sunrise.Events;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Content.Shared.Weapons.Reflect;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using Robust.Shared.Containers;

namespace Content.Server.Weapons.Ranged.Systems;

public sealed partial class GunSystem : SharedGunSystem
{
    [Dependency] private readonly DamageExamineSystem _damageExamine = default!;
    [Dependency] private readonly PricingSystem _pricing = default!;
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly HandsSystem _hands = default!;

    private const float DamagePitchVariation = 0.05f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BallisticAmmoProviderComponent, PriceCalculationEvent>(OnBallisticPrice);
    }

    private void OnBallisticPrice(EntityUid uid,
        BallisticAmmoProviderComponent component,
        ref PriceCalculationEvent args)
    {
        if (string.IsNullOrEmpty(component.Proto) || component.UnspawnedCount == 0)
            return;

        if (!ProtoManager.TryIndex<EntityPrototype>(component.Proto, out var proto))
        {
            Log.Error($"Unable to find fill prototype for price on {component.Proto} on {ToPrettyString(uid)}");
            return;
        }

        // Probably good enough for most.
        var price = _pricing.GetEstimatedPrice(proto);
        args.Price += price * component.UnspawnedCount;
    }

    public override void Shoot(EntityUid gunUid,
        GunComponent gun,
        List<(EntityUid? Entity, IShootable Shootable)> ammo,
        EntityCoordinates fromCoordinates,
        EntityCoordinates toCoordinates,
        out bool userImpulse,
        EntityUid? user = null,
        bool throwItems = false)
    {
        userImpulse = true;

        if (user != null)
        {
            var selfEvent = new SelfBeforeGunShotEvent(user.Value, (gunUid, gun), ammo);
            RaiseLocalEvent(user.Value, selfEvent);
            if (selfEvent.Cancelled)
            {
                userImpulse = false;
                return;
            }
        }

        var fromMap = TransformSystem.ToMapCoordinates(fromCoordinates);
        var toMap = TransformSystem.ToMapCoordinates(toCoordinates).Position;
        var mapDirection = toMap - fromMap.Position;
        var mapAngle = mapDirection.ToAngle();
        var angle = GetRecoilAngle(Timing.CurTime, gun, mapDirection.ToAngle());

        // If applicable, this ensures the projectile is parented to grid on spawn, instead of the map.
        var fromEnt = MapManager.TryFindGridAt(fromMap, out var gridUid, out _)
            ? TransformSystem.WithEntityId(fromCoordinates, gridUid)
            : new EntityCoordinates(_map.GetMapOrInvalid(fromMap.MapId), fromMap.Position);

        // Update shot based on the recoil
        toMap = fromMap.Position + angle.ToVec() * mapDirection.Length();
        mapDirection = toMap - fromMap.Position;
        var gunVelocity = Physics.GetMapLinearVelocity(fromEnt);

        // I must be high because this was getting tripped even when true.
        // DebugTools.Assert(direction != Vector2.Zero);
        var shotProjectiles = new List<EntityUid>(ammo.Count);

        foreach (var (ent, shootable) in ammo)
        {
            // pneumatic cannon doesn't shoot bullets it just throws them, ignore ammo handling
            if (throwItems && ent != null)
            {
                ShootOrThrow(ent.Value, mapDirection, gunVelocity, gun, gunUid, user);
                continue;
            }

            switch (shootable)
            {
                // Cartridge shoots something else
                case CartridgeAmmoComponent cartridge:
                    if (!cartridge.Spent)
                    {
                        var uid = Spawn(cartridge.Prototype, fromEnt);
                        CreateAndFireProjectiles(uid, cartridge);

                        RaiseLocalEvent(ent!.Value,
                            new AmmoShotEvent
                            {
                                FiredProjectiles = shotProjectiles,
                            });

                        SetCartridgeSpent(ent.Value, cartridge, true);

                        if (cartridge.DeleteOnSpawn)
                            Del(ent.Value);
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.SoundEmpty, gunUid, user);
                    }

                    // Something like ballistic might want to leave it in the container still
                    if (!cartridge.DeleteOnSpawn && !Containers.IsEntityInContainer(ent!.Value))
                        EjectCartridge(ent.Value, angle);

                    Dirty(ent!.Value, cartridge);
                    break;
                case HitScanCartridgeAmmoComponent hitScanCartridge:
                    if (!hitScanCartridge.Spent)
                    {
                        var hitscanProto = ProtoManager.Index<HitscanPrototype>(hitScanCartridge.Prototype);
                        if (hitscanProto.ShootModifier == ShootModifier.Split)
                        {
                            var perpendicularOffset = new Vector2(-mapDirection.Y, mapDirection.X).Normalized();

                            for (var i = 0; i < hitscanProto.SplitCount; i++)
                            {
                                var offset = hitscanProto.SplitOffset * (i - (hitscanProto.SplitCount - 1) / 2.0f);

                                var startCoordinates = fromCoordinates.Offset(perpendicularOffset * offset);
                                var startMapCoordinates = fromMap.Offset(perpendicularOffset * offset);

                                HitscanShoot(startMapCoordinates,
                                    startCoordinates,
                                    mapDirection,
                                    hitscanProto,
                                    gunUid,
                                    user,
                                    gun);
                            }
                        }
                        else if (hitscanProto.ShootModifier == ShootModifier.Spread)
                        {
                            var spreadEvent = new GunGetAmmoSpreadEvent(hitscanProto.SpreadAngle);
                            RaiseLocalEvent(gunUid, ref spreadEvent);

                            var angles = LinearSpread(mapAngle - spreadEvent.Spread / 2,
                                mapAngle + spreadEvent.Spread / 2,
                                hitscanProto.SpreadCount);

                            HitscanShoot(fromMap,
                                fromCoordinates,
                                angles[0].ToVec(),
                                hitscanProto,
                                gunUid,
                                user,
                                gun,
                                ent!.Value);

                            for (var i = 1; i < hitscanProto.SpreadCount; i++)
                            {
                                HitscanShoot(fromMap,
                                    fromCoordinates,
                                    angles[i].ToVec(),
                                    hitscanProto,
                                    gunUid,
                                    user,
                                    gun,
                                    ent.Value);
                            }
                        }
                        else
                        {
                            HitscanShoot(fromMap,
                                fromCoordinates,
                                mapDirection,
                                hitscanProto,
                                gunUid,
                                user,
                                gun,
                                ent!.Value);
                        }

                        SetHitscanCartridgeSpent(ent!.Value, hitScanCartridge, true);

                        if (hitScanCartridge.DeleteOnSpawn)
                            Del(ent.Value);
                    }
                    else
                    {
                        userImpulse = false;
                        Audio.PlayPredicted(gun.SoundEmpty, gunUid, user);
                    }

                    if (!hitScanCartridge.DeleteOnSpawn && !Containers.IsEntityInContainer(ent!.Value))
                        EjectCartridge(ent.Value, angle);

                    Dirty(ent!.Value, hitScanCartridge);
                    break;
                // Ammo shoots itself
                case AmmoComponent newAmmo:
                    if (ent == null)
                        break;
                    CreateAndFireProjectiles(ent.Value, newAmmo);

                    break;
                case HitscanPrototype hitscan:
                    if (hitscan.ShootModifier == ShootModifier.Split)
                    {
                        var perpendicularOffset = new Vector2(-mapDirection.Y, mapDirection.X).Normalized();

                        for (var i = 0; i < hitscan.SplitCount; i++)
                        {
                            var offset = hitscan.SplitOffset * (i - (hitscan.SplitCount - 1) / 2.0f);

                            var startCoordinates = fromCoordinates.Offset(perpendicularOffset * offset);
                            var startMapCoordinates = fromMap.Offset(perpendicularOffset * offset);

                            HitscanShoot(startMapCoordinates,
                                startCoordinates,
                                mapDirection,
                                hitscan,
                                gunUid,
                                user,
                                gun);
                        }
                    }
                    else if (hitscan.ShootModifier == ShootModifier.Spread)
                    {
                        var spreadEvent = new GunGetAmmoSpreadEvent(hitscan.SpreadAngle);
                        RaiseLocalEvent(gunUid, ref spreadEvent);

                        var angles = LinearSpread(mapAngle - spreadEvent.Spread / 2,
                            mapAngle + spreadEvent.Spread / 2,
                            hitscan.SpreadCount);

                        HitscanShoot(fromMap, fromCoordinates, angles[0].ToVec(), hitscan, gunUid, user, gun);

                        for (var i = 1; i < hitscan.SpreadCount; i++)
                        {
                            HitscanShoot(fromMap, fromCoordinates, angles[i].ToVec(), hitscan, gunUid, user, gun);
                        }
                    }
                    else
                    {
                        HitscanShoot(fromMap, fromCoordinates, mapDirection, hitscan, gunUid, user, gun);
                    }

                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        RaiseLocalEvent(gunUid,
            new AmmoShotEvent
            {
                FiredProjectiles = shotProjectiles,
            });

        void CreateAndFireProjectiles(EntityUid ammoEnt, AmmoComponent ammoComp)
        {
            if (TryComp<ProjectileSpreadComponent>(ammoEnt, out var ammoSpreadComp))
            {
                var spreadEvent = new GunGetAmmoSpreadEvent(ammoSpreadComp.Spread);
                RaiseLocalEvent(gunUid, ref spreadEvent);

                var angles = LinearSpread(mapAngle - spreadEvent.Spread / 2,
                    mapAngle + spreadEvent.Spread / 2,
                    ammoSpreadComp.Count);

                ShootOrThrow(ammoEnt, angles[0].ToVec(), gunVelocity, gun, gunUid, user);
                shotProjectiles.Add(ammoEnt);

                for (var i = 1; i < ammoSpreadComp.Count; i++)
                {
                    var newuid = Spawn(ammoSpreadComp.Proto, fromEnt);
                    ShootOrThrow(newuid, angles[i].ToVec(), gunVelocity, gun, gunUid, user);
                    shotProjectiles.Add(newuid);
                }
            }
            else
            {
                ShootOrThrow(ammoEnt, mapDirection, gunVelocity, gun, gunUid, user);
                shotProjectiles.Add(ammoEnt);
            }

            MuzzleFlash(gunUid, ammoComp, mapDirection.ToAngle(), user);
            Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);
        }
    }


    private void HitscanShoot(MapCoordinates fromMap,
        EntityCoordinates fromCoordinates,
        Vector2 mapDirection,
        HitscanPrototype hitscan,
        EntityUid gunUid,
        EntityUid? user,
        GunComponent gun,
        EntityUid? ammoEnt = null)
    {
        EntityUid? lastHit = null;
        var from = fromMap;
        // can't use map coords above because funny FireEffects
        var fromEffect = fromCoordinates;
        var dir = mapDirection.Normalized();

        //in the situation when user == null, means that the cannon fires on its own (via signals). And we need the gun to not fire by itself in this case
        var lastUser = user ?? gunUid;
        EntityUid? hitEntity = null;

        List<EntityUid> ignoredEntity = new();

        if (TryComp<StrapComponent>(lastUser, out var strapComponent))
        {
            ignoredEntity.AddRange(strapComponent.BuckledEntities);
        }

        if (TryComp<BuckleComponent>(lastUser, out var buckleComponent) && buckleComponent.BuckledTo != null)
        {
            ignoredEntity.Add(buckleComponent.BuckledTo.Value);
        }

        if (hitscan.Reflective != ReflectType.None)
        {
            for (var reflectAttempt = 0; reflectAttempt < 3; reflectAttempt++)
            {
                var ray = new CollisionRay(from.Position, dir, (int)hitscan.CollisionMask);
                var rayCastResults =
                    Physics.IntersectRay(from.MapId, ray, hitscan.MaxLength, lastUser, false).ToList();
                if (!rayCastResults.Any())
                    break;

                foreach (var rayCastResult in rayCastResults.ToList())
                {
                    if (ignoredEntity.Contains(rayCastResult.HitEntity))
                        rayCastResults.Remove(rayCastResult);
                }

                var result = rayCastResults[0];

                // Check if laser is shot from in a container
                if (!_container.IsEntityOrParentInContainer(lastUser))
                {
                    // Checks if the laser should pass over unless targeted by its user
                    foreach (var collide in rayCastResults)
                    {
                        if (!gun.Targets.Contains(collide.HitEntity) &&
                            CompOrNull<RequireProjectileTargetComponent>(collide.HitEntity)?.Active == true)
                        {
                            continue;
                        }

                        result = collide;
                        break;
                    }
                }

                var hit = result.HitEntity;
                lastHit = hit;

                FireEffects(fromEffect, result.Distance, dir.Normalized().ToAngle(), hitscan, hit);

                var ev = new HitScanReflectAttemptEvent(user, gunUid, hitscan.Reflective, dir, false);
                RaiseLocalEvent(hit, ref ev);

                if (!ev.Reflected)
                    break;

                // Sunrise-start
                // Эта логика используется для зеркального щита культистов
                var reflectedEv = new ReflectedEvent(user, gunUid, hitscan.Damage, hitscan.Reflective);
                foreach (var hand in _hands.EnumerateHeld(hit))
                {
                    RaiseLocalEvent(hand, reflectedEv);
                }

                RaiseLocalEvent(hit, reflectedEv);
                // Sunrise-end

                fromEffect = Transform(hit).Coordinates;
                from = TransformSystem.ToMapCoordinates(fromEffect);
                dir = ev.Direction;
                lastUser = hit;
            }
        }

        if (lastHit != null)
        {
            hitEntity = lastHit.Value;
            if (hitscan.StaminaDamage > 0f)
                _stamina.TakeStaminaDamage(hitEntity.Value, hitscan.StaminaDamage, source: user);

            var dmg = hitscan.Damage;

            var hitName = ToPrettyString(hitEntity);
            if (dmg != null)
            {
                dmg = Damageable.TryChangeDamage(hitEntity,
                    dmg * Damageable.UniversalHitscanDamageModifier,
                    hitscan.IgnoreResistances,
                    origin: user);
            }

            // check null again, as TryChangeDamage returns modified damage values
            if (dmg != null)
            {
                if (!Deleted(hitEntity))
                {
                    if (dmg.AnyPositive())
                    {
                        _color.RaiseEffect(Color.Red,
                            new List<EntityUid> { hitEntity.Value },
                            Filter.Pvs(hitEntity.Value, entityManager: EntityManager));
                    }

                    // TODO get fallback position for playing hit sound.
                    PlayImpactSound(hitEntity.Value, dmg, hitscan.Sound, hitscan.ForceSound);
                }

                if (user != null)
                {
                    Logs.Add(LogType.HitScanHit,
                        $"{ToPrettyString(user.Value):user} hit {hitName:target} using hitscan and dealt {dmg.GetTotal():damage} damage");
                }
                else
                {
                    Logs.Add(LogType.HitScanHit,
                        $"{hitName:target} hit by hitscan dealing {dmg.GetTotal():damage} damage");
                }
            }
        }
        else
        {
            FireEffects(fromEffect, hitscan.MaxLength, dir.ToAngle(), hitscan);
        }

        Audio.PlayPredicted(gun.SoundGunshotModified, gunUid, user);

        if (hitEntity == null || ammoEnt == null)
            return;

        RaiseLocalEvent(ammoEnt.Value,
            new HitscanAmmoShotEvent
            {
                Target = hitEntity.Value,
                Shooter = user,
            });
    }

    private void ShootOrThrow(EntityUid uid,
        Vector2 mapDirection,
        Vector2 gunVelocity,
        GunComponent gun,
        EntityUid gunUid,
        EntityUid? user)
    {
        if (gun.Targets.Count > 0 && !TerminatingOrDeleted(gun.Targets.First()))
        {
            var targeted = EnsureComp<TargetedProjectileComponent>(uid);
            targeted.Targets = new(gun.Targets);
            Dirty(uid, targeted);
        }

        // Do a throw
        if (!HasComp<ProjectileComponent>(uid))
        {
            RemoveShootable(uid);
            // TODO: Someone can probably yeet this a billion miles so need to pre-validate input somewhere up the call stack.
            ThrowingSystem.TryThrow(uid, mapDirection, gun.ProjectileSpeedModified, user);
            return;
        }

        ShootProjectile(uid, mapDirection, gunVelocity, gunUid, user, gun.ProjectileSpeedModified);
    }

    /// <summary>
    /// Gets a linear spread of angles between start and end.
    /// </summary>
    /// <param name="start">Start angle in degrees</param>
    /// <param name="end">End angle in degrees</param>
    /// <param name="intervals">How many shots there are</param>
    private Angle[] LinearSpread(Angle start, Angle end, int intervals)
    {
        var angles = new Angle[intervals];
        DebugTools.Assert(intervals > 1);

        for (var i = 0; i <= intervals - 1; i++)
        {
            angles[i] = new Angle(start + (end - start) * i / (intervals - 1));
        }

        return angles;
    }

    private Angle GetRecoilAngle(TimeSpan curTime, GunComponent component, Angle direction)
    {
        var timeSinceLastFire = (curTime - component.LastFire).TotalSeconds;
        var newTheta =
            MathHelper.Clamp(
                component.CurrentAngle.Theta + component.AngleIncreaseModified.Theta -
                component.AngleDecayModified.Theta * timeSinceLastFire,
                component.MinAngleModified.Theta,
                component.MaxAngleModified.Theta);
        component.CurrentAngle = new Angle(newTheta);
        component.LastFire = component.NextFire;

        // Convert it so angle can go either side.
        var random = Random.NextFloat(-0.5f, 0.5f);
        var spread = component.CurrentAngle.Theta * random;
        var angle = new Angle(direction.Theta + component.CurrentAngle.Theta * random);
        DebugTools.Assert(spread <= component.MaxAngleModified.Theta);
        return angle;
    }

    protected override void Popup(string message, EntityUid? uid, EntityUid? user) { }

    protected override void CreateEffect(EntityUid gunUid, MuzzleFlashEvent message, EntityUid? user = null)
    {
        var filter = Filter.Pvs(gunUid, entityManager: EntityManager);

        if (TryComp<ActorComponent>(user, out var actor))
            filter.RemovePlayer(actor.PlayerSession);

        RaiseNetworkEvent(message, filter);
    }

    public void PlayImpactSound(EntityUid otherEntity,
        DamageSpecifier? modifiedDamage,
        SoundSpecifier? weaponSound,
        bool forceWeaponSound)
    {
        DebugTools.Assert(!Deleted(otherEntity), "Impact sound entity was deleted");

        // Like projectiles and melee,
        // 1. Entity specific sound
        // 2. Ammo's sound
        // 3. Nothing
        var playedSound = false;

        if (!forceWeaponSound && modifiedDamage != null && modifiedDamage.GetTotal() > 0 &&
            TryComp<RangedDamageSoundComponent>(otherEntity, out var rangedSound))
        {
            var type = SharedMeleeWeaponSystem.GetHighestDamageSound(modifiedDamage, ProtoManager);

            if (type != null && rangedSound.SoundTypes?.TryGetValue(type, out var damageSoundType) == true)
            {
                Audio.PlayPvs(damageSoundType, otherEntity, AudioParams.Default.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
            else if (type != null && rangedSound.SoundGroups?.TryGetValue(type, out var damageSoundGroup) == true)
            {
                Audio.PlayPvs(damageSoundGroup, otherEntity, AudioParams.Default.WithVariation(DamagePitchVariation));
                playedSound = true;
            }
        }

        if (!playedSound && weaponSound != null)
        {
            Audio.PlayPvs(weaponSound, otherEntity);
        }
    }

    // TODO: Pseudo RNG so the client can predict these.

    #region Hitscan effects

    private void FireEffects(EntityCoordinates fromCoordinates,
        float distance,
        Angle angle,
        HitscanPrototype hitscan,
        EntityUid? hitEntity = null)
    {
        // Lord
        // Forgive me for the shitcode I am about to do
        // Effects tempt me not
        var sprites =
            new List<(NetCoordinates coordinates, Angle angle, SpriteSpecifier sprite, float scale, EffectType)>();
        var fromXform = Transform(fromCoordinates.EntityId);

        // We'll get the effects relative to the grid / map of the firer
        // Look you could probably optimise this a bit with redundant transforms at this point.

        var gridUid = fromXform.GridUid;
        if (gridUid != fromCoordinates.EntityId && TryComp(gridUid, out TransformComponent? gridXform))
        {
            var (_, gridRot, gridInvMatrix) = TransformSystem.GetWorldPositionRotationInvMatrix(gridXform);
            var map = TransformSystem.ToMapCoordinates(fromCoordinates);
            fromCoordinates = new EntityCoordinates(gridUid.Value, Vector2.Transform(map.Position, gridInvMatrix));
            angle -= gridRot;
        }
        else
        {
            angle -= TransformSystem.GetWorldRotation(fromXform);
        }

        if (distance >= 1f)
        {
            if (hitscan.MuzzleFlash != null)
            {
                var coords = fromCoordinates.Offset(angle.ToVec().Normalized() / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, angle, hitscan.MuzzleFlash, 1f, EffectType.Static));
            }

            if (hitscan.TravelFlash != null)
            {
                var coords = fromCoordinates.Offset(angle.ToVec() * (distance + 0.5f) / 2);
                var netCoords = GetNetCoordinates(coords);

                sprites.Add((netCoords, angle, hitscan.TravelFlash, distance - 1.5f, EffectType.Static));
            }

            if (hitscan.BulletTracer != null)
            {
                var coords = fromCoordinates.Offset(angle.ToVec().Normalized() / 2);
                var netCoords = GetNetCoordinates(coords);
                sprites.Add((netCoords, angle, hitscan.BulletTracer, distance - 1.5f, EffectType.Tracer));
            }
        }

        if (hitscan.ImpactFlash != null)
        {
            var coords = fromCoordinates.Offset(angle.ToVec() * distance);
            var netCoords = GetNetCoordinates(coords);

            sprites.Add((netCoords, angle.FlipPositive(), hitscan.ImpactFlash, 1f, EffectType.Static));
        }

        if (sprites.Count > 0)
        {
            RaiseNetworkEvent(new HitscanEvent
                {
                    Sprites = sprites,
                },
                Filter.Pvs(fromCoordinates, entityMan: EntityManager));
        }
    }

    #endregion
}
