using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Weapons.Ranged;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._Sunrise.Silicons.Borgs.Systems;

/// <summary>
/// Shared-side borg weapon power routing for correct client/server behavior.
/// </summary>
public sealed class SharedBorgWeaponPowerSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    private TimeSpan _nextSync = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BatteryAmmoProviderComponent, BatteryAmmoProviderChargeAttemptEvent>(OnBatteryChargeAttempt);
    }

    private void OnBatteryChargeAttempt(Entity<BatteryAmmoProviderComponent> ent, ref BatteryAmmoProviderChargeAttemptEvent args)
    {
        if (args.TotalChargeCost <= 0f || args.User is not { } user)
            return;

        if (!TryComp<BorgChassisComponent>(user, out var chassis))
            return;

        if (!IsSelectedModuleWeapon(ent.Owner, user, chassis))
            return;

        if (!TryComp<PowerCellSlotComponent>(user, out var cellSlot))
            return;

        SyncBorgWeaponShots(ent, user, cellSlot);

        if (!_powerCell.HasCharge((user, cellSlot), args.TotalChargeCost))
        {
            args.Cancelled = true;
            args.Reason = Loc.GetString("power-cell-insufficient");
            return;
        }

        if (_net.IsServer && !_powerCell.TryUseCharge((user, cellSlot), args.TotalChargeCost))
        {
            args.Cancelled = true;
            args.Reason = Loc.GetString("power-cell-insufficient");
            return;
        }

        // Borg module weapons should use chassis battery instead of local weapon battery.
        args.UseLocalCharge = false;

        SyncBorgWeaponShots(ent, user, cellSlot);
    }

    private bool IsSelectedModuleWeapon(EntityUid weapon, EntityUid borg, BorgChassisComponent chassis)
    {
        if (chassis.SelectedModule is not { } selectedModule)
            return false;

        if (!TryComp<ItemBorgModuleComponent>(selectedModule, out var itemModule))
            return false;

        if (!HasComp<GunComponent>(weapon))
            return false;

        foreach (var item in EnumerateModuleItems(borg, (selectedModule, itemModule)))
        {
            if (item == weapon)
                return true;
        }

        return false;
    }

    private IEnumerable<EntityUid> EnumerateModuleItems(EntityUid chassis, Entity<ItemBorgModuleComponent> module)
    {
        var yielded = new HashSet<EntityUid>();

        if (TryComp<HandsComponent>(chassis, out var hands))
        {
            for (var i = 0; i < module.Comp.Hands.Count; i++)
            {
                var handId = $"{GetNetEntity(module.Owner)}-hand-{i}";
                if (!_hands.TryGetHeldItem((chassis, hands), handId, out var held))
                    continue;

                if (yielded.Add(held.Value))
                    yield return held.Value;
            }
        }

        foreach (var item in module.Comp.StoredItems.Values)
        {
            if (yielded.Add(item))
                yield return item;
        }
    }

    private void SyncBorgWeaponShots(Entity<BatteryAmmoProviderComponent> weapon, EntityUid borg, PowerCellSlotComponent cellSlot)
    {
        var shots = _powerCell.GetRemainingUses((borg, cellSlot), weapon.Comp.FireCost);
        var capacity = _powerCell.GetMaxUses((borg, cellSlot), weapon.Comp.FireCost);

        if (weapon.Comp.Shots == shots && weapon.Comp.Capacity == capacity)
            return;

        weapon.Comp.Shots = shots;
        weapon.Comp.Capacity = capacity;

        if (TryComp<AppearanceComponent>(weapon, out var appearance))
        {
            _appearance.SetData(weapon.Owner, AmmoVisuals.HasAmmo, shots > 0, appearance);
            _appearance.SetData(weapon.Owner, AmmoVisuals.AmmoCount, shots, appearance);
            _appearance.SetData(weapon.Owner, AmmoVisuals.AmmoMax, capacity, appearance);
        }

        if (_net.IsServer)
            Dirty(weapon);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime < _nextSync)
            return;

        _nextSync = curTime + TimeSpan.FromSeconds(0.5);

        var borgQuery = EntityQueryEnumerator<BorgChassisComponent, PowerCellSlotComponent>();
        while (borgQuery.MoveNext(out var borgUid, out var chassis, out var slot))
        {
            if (chassis.SelectedModule is not { } selectedModule)
                continue;

            if (!TryComp<ItemBorgModuleComponent>(selectedModule, out var module))
                continue;

            foreach (var item in EnumerateModuleItems(borgUid, (selectedModule, module)))
            {
                if (!TryComp<BatteryAmmoProviderComponent>(item, out var provider))
                    continue;

                SyncBorgWeaponShots((item, provider), borgUid, slot);
            }
        }
    }
}
