using Content.Shared.Projectiles;
using Content.Shared.Weapons.Hitscan.Events;
using Robust.Shared.Network;
using Robust.Shared.Random;

namespace Content.Shared._Sunrise.Weapons.Ranged;

public sealed class BulletholeSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const int MaxBulletholeState = 10;
    private const int MaxBulletholeCount = 24;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BulletholeGeneratorComponent, ProjectileHitEvent>(OnProjectileHit);
        SubscribeLocalEvent<BulletholeGeneratorComponent, HitscanRaycastFiredEvent>(OnHitscanHit);
    }

    private void OnProjectileHit(Entity<BulletholeGeneratorComponent> ent, ref ProjectileHitEvent args)
    {
        if (!_net.IsServer)
            return;

        TryApplyBullethole(args.Target, ent.Comp.RsiPath);
    }

    private void OnHitscanHit(Entity<BulletholeGeneratorComponent> ent, ref HitscanRaycastFiredEvent args)
    {
        if (!_net.IsServer)
            return;

        if (args.Data.HitEntity is not { } target)
            return;

        TryApplyBullethole(target, ent.Comp.RsiPath);
    }

    private void TryApplyBullethole(EntityUid target, string rsiPath)
    {
        if (!TryComp<BulletholeComponent>(target, out var bullethole))
            return;

        bullethole.BulletholeCount++;

        if (!TryComp<AppearanceComponent>(target, out var appearance))
            return;

        if (bullethole.BulletholeState < 1 || bullethole.BulletholeState > MaxBulletholeState)
            bullethole.BulletholeState = _random.Next(1, MaxBulletholeState + 1);

        var displayState = bullethole.BulletholeState;
        var displayCount = Math.Min(bullethole.BulletholeCount, MaxBulletholeCount);
        var state = $"bhole_{displayState}_{displayCount}";

        Dirty(target, bullethole);
        _appearance.SetData(target, BulletholeVisuals.RsiPath, rsiPath, appearance);
        _appearance.SetData(target, BulletholeVisuals.State, state, appearance);
    }
}
