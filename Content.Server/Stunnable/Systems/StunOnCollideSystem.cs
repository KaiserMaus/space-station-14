using Content.Server._Sunrise.Stunnable.Components;
using Content.Server.Stunnable.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Hitscan.Events;
using JetBrains.Annotations;
using Robust.Shared.Physics.Events;

namespace Content.Server.Stunnable.Systems;

[UsedImplicitly]
internal sealed partial class StunOnCollideSystem : EntitySystem
{
    [Dependency] private StunSystem _stunSystem = default!;
    [Dependency] private MovementModStatusSystem _movementMod = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StunOnCollideComponent, StartCollideEvent>(HandleCollide);
        SubscribeLocalEvent<StunOnCollideComponent, ThrowDoHitEvent>(HandleThrow);
        // Sunrise-Edit - поддержка останавливающего действия для hitscan-боеприпасов.
        SubscribeLocalEvent<StunOnCollideComponent, HitscanRaycastFiredEvent>(HandleHitscan);
    }

    private void TryDoCollideStun(Entity<StunOnCollideComponent> ent, EntityUid target)
    {
        // Sunrise edit start - ПП не должны применять останавливающее действие.
        if (TryComp<ProjectileComponent>(ent, out var projectile) &&
            projectile.Weapon is { } weapon &&
            HasComp<SuppressStoppingPowerComponent>(weapon))
        {
            return;
        }
        // Sunrise edit end

        _stunSystem.TryKnockdown(target, ent.Comp.KnockdownAmount, ent.Comp.Refresh, ent.Comp.AutoStand, ent.Comp.Drop, true);

        if (ent.Comp.Refresh)
        {
            _stunSystem.TryUpdateStunDuration(target, ent.Comp.StunAmount);

            _movementMod.TryUpdateMovementSpeedModDuration(
                target,
                MovementModStatusSystem.TaserSlowdown,
                ent.Comp.SlowdownAmount,
                ent.Comp.WalkSpeedModifier,
                ent.Comp.SprintSpeedModifier
            );
        }
        else
        {
            _stunSystem.TryAddStunDuration(target, ent.Comp.StunAmount);
            _movementMod.TryAddMovementSpeedModDuration(
                target,
                MovementModStatusSystem.TaserSlowdown,
                ent.Comp.SlowdownAmount,
                ent.Comp.WalkSpeedModifier,
                ent.Comp.SprintSpeedModifier
            );
        }
    }

    private void HandleCollide(Entity<StunOnCollideComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureID)
            return;

        TryDoCollideStun(ent, args.OtherEntity);
    }

    private void HandleThrow(Entity<StunOnCollideComponent> ent, ref ThrowDoHitEvent args)
    {
        TryDoCollideStun(ent, args.Target);
    }

    // Sunrise added start - применение останавливающего действия при hitscan-попадании.
    private void HandleHitscan(Entity<StunOnCollideComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (args.Data.HitEntity is not { } target ||
            HasComp<SuppressStoppingPowerComponent>(args.Data.Gun))
        {
            return;
        }

        TryDoCollideStun(ent, target);
    }
    // Sunrise added end
}
