using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared._Sunrise.Silicons.Borgs.Events;

namespace Content.Server._Sunrise.Silicons.Borgs.Systems;

/// <summary>
/// Redirects energy weapon power usage for borg module weapons to the borg chassis battery.
/// </summary>
public sealed class BorgWeaponPowerSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;

    private readonly Dictionary<EntityUid, float> _originalAutoRechargeRates = new();
    private readonly Dictionary<EntityUid, HashSet<EntityUid>> _moduleItems = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ItemBorgModuleComponent, BorgModuleItemsToggledEvent>(OnModuleItemsToggled);
    }

    private void OnModuleItemsToggled(Entity<ItemBorgModuleComponent> module, ref BorgModuleItemsToggledEvent args)
    {
        if (args.Enabled)
            OnModuleSelected(module, args.Chassis);
        else
            OnModuleUnselected(module);
    }

    private void OnModuleSelected(Entity<ItemBorgModuleComponent> module, EntityUid chassis)
    {
        var trackedItems = GetOrCreateTrackedItems(module.Owner);
        trackedItems.Clear();

        foreach (var item in EnumerateModuleItems(chassis, module))
        {
            trackedItems.Add(item);

            if (!TryComp<BatterySelfRechargerComponent>(item, out var selfRecharger))
                continue;

            if (!_originalAutoRechargeRates.ContainsKey(item))
                _originalAutoRechargeRates[item] = selfRecharger.AutoRechargeRate;

            selfRecharger.AutoRechargeRate = 0f;
            Dirty(item, selfRecharger);
            _battery.RefreshChargeRate(item);
        }
    }

    private void OnModuleUnselected(Entity<ItemBorgModuleComponent> module)
    {
        if (!_moduleItems.TryGetValue(module.Owner, out var trackedItems))
            return;

        foreach (var item in trackedItems)
        {
            if (!_originalAutoRechargeRates.TryGetValue(item, out var originalRate))
                continue;

            if (!TryComp<BatterySelfRechargerComponent>(item, out var selfRecharger))
            {
                _originalAutoRechargeRates.Remove(item);
                continue;
            }

            selfRecharger.AutoRechargeRate = originalRate;
            Dirty(item, selfRecharger);
            _battery.RefreshChargeRate(item);
            _originalAutoRechargeRates.Remove(item);
        }

        _moduleItems.Remove(module.Owner);
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

    private HashSet<EntityUid> GetOrCreateTrackedItems(EntityUid module)
    {
        if (_moduleItems.TryGetValue(module, out var trackedItems))
            return trackedItems;

        trackedItems = new HashSet<EntityUid>();
        _moduleItems[module] = trackedItems;
        return trackedItems;
    }
}
