using Content.Shared._Sunrise.Mech;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Mech.Components;
using Content.Shared.Popups;
using Robust.Server.Containers;
using Robust.Shared.Containers;

namespace Content.Server._Sunrise.Mech;

public sealed partial class SunriseMechSystem
{
    [Dependency] private readonly ContainerSystem _container = default!;

    private void InitializePaddyUpgrade()
    {
        SubscribeLocalEvent<MechUpgradeKitComponent, AfterInteractEvent>(OnMechUpgradeKitAfterInteract);
        SubscribeLocalEvent<MechUpgradeKitComponent, MechUpgradeKitDoAfterEvent>(OnMechUpgradeKitDoAfter);
    }

    private void OnMechUpgradeKitAfterInteract(Entity<MechUpgradeKitComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target == null)
            return;

        if (!TryComp<MechComponent>(args.Target, out var mech))
            return;

        if (mech.PilotSlot.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("mech-upgrade-pilot-inside"), args.User, args.User, PopupType.Medium);
            return;
        }

        var prototypeId = MetaData(args.Target.Value).EntityPrototype?.ID;
        if (prototypeId == null || !ent.Comp.TargetPrototypes.Contains(prototypeId))
        {
            _popup.PopupEntity(Loc.GetString("mech-upgrade-invalid-target"), args.User, args.User, PopupType.Medium);
            return;
        }

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new MechUpgradeKitDoAfterEvent(), ent, target: args.Target, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnHandChange = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    private void OnMechUpgradeKitDoAfter(Entity<MechUpgradeKitComponent> ent, ref MechUpgradeKitDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null)
            return;

        if (!TryComp<MechComponent>(args.Target, out var mech))
            return;

        if (mech.PilotSlot.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("mech-upgrade-pilot-inside"), args.User, args.User, PopupType.Medium);
            return;
        }

        var prototypeId = MetaData(args.Target.Value).EntityPrototype?.ID;
        if (prototypeId == null || !ent.Comp.TargetPrototypes.Contains(prototypeId))
        {
            _popup.PopupEntity(Loc.GetString("mech-upgrade-invalid-target"), args.User, args.User, PopupType.Medium);
            return;
        }

        var coordinates = Transform(args.Target.Value).Coordinates;
        var battery = mech.BatterySlot.ContainedEntity;
        var equipment = new List<EntityUid>(mech.EquipmentContainer.ContainedEntities);

        var newMech = Spawn(ent.Comp.ResultPrototype, coordinates);
        if (!TryComp<MechComponent>(newMech, out var newMechComp))
            return;

        if (battery != null)
        {
            _container.Remove(battery.Value, mech.BatterySlot, force: true);
            _container.Insert(battery.Value, newMechComp.BatterySlot);
        }

        foreach (var item in equipment)
        {
            _container.Remove(item, mech.EquipmentContainer, force: true);
            _mech.InsertEquipment(newMech, item, newMechComp);
        }

        Del(args.Target.Value);
        Del(ent);

        _popup.PopupEntity(Loc.GetString("mech-upgrade-success"), args.User, args.User, PopupType.Medium);
        args.Handled = true;
    }
}
