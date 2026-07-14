using Content.Server.Popups;
using Content.Server.Radio.EntitySystems;
using Content.Server.Shuttles;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Shipyard;
using Content.Shared._Sunrise.Shipyard.BUI;
using Content.Shared._Sunrise.Shipyard.Components;
using Content.Shared._Sunrise.Shipyard.Events;
using Content.Shared._Sunrise.Shipyard.Prototypes;
using Content.Shared.Access.Systems;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Shipyard;

public sealed class ShipyardSystem : EntitySystem
{
    private const string InvalidVesselMessage = "shipyard-console-invalid-vessel";

    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCargoSystem _cargo = default!;
    [Dependency] private readonly DockingSystem _docking = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        Subs.BuiEvents<ShipyardConsoleComponent>(ShipyardConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnUiOpened);
            subs.Event<ShipyardConsolePurchaseMessage>(OnPurchase);
            subs.Event<ShipyardConsoleSellMessage>(OnSell);
        });
    }

    private void OnUiOpened(Entity<ShipyardConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnPurchase(Entity<ShipyardConsoleComponent> ent, ref ShipyardConsolePurchaseMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent))
        {
            Deny(ent, args.Actor, "shipyard-console-access-denied");
            return;
        }

        if (ent.Comp.CurrentShuttle is { } current && Exists(current))
        {
            Deny(ent, args.Actor, "shipyard-console-sell-first");
            return;
        }

        if (!TryGetVessel(ent.Comp, args.VesselId, out var vessel))
        {
            Deny(ent, args.Actor, InvalidVesselMessage);
            return;
        }

        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
        {
            Deny(ent, args.Actor, "shipyard-console-station-not-found");
            return;
        }

        if (!_cargo.TryGetAccount((station, bank), ent.Comp.Account, out var balance))
        {
            Deny(ent, args.Actor, "shipyard-console-account-not-found");
            return;
        }

        if (vessel.Price < 0 || balance < vessel.Price)
        {
            Deny(ent, args.Actor, "shipyard-console-insufficient-funds", ("cost", vessel.Price));
            return;
        }

        var mapId = Transform(ent).MapID;
        var coordinates = _transform.GetWorldPosition(ent) + vessel.SpawnOffset;
        if (!_mapLoader.TryLoadGrid(mapId, vessel.GridPath, out var shuttleGrid, offset: coordinates, rot: vessel.Rotation))
        {
            Deny(ent, args.Actor, "shipyard-console-load-failed");
            return;
        }

        if (!_cargo.TryAdjustBankAccount((station, bank), ent.Comp.Account, -vessel.Price))
        {
            QueueDel(shuttleGrid.Value.Owner);
            Deny(ent, args.Actor, "shipyard-console-account-not-found");
            return;
        }

        var shuttleUid = shuttleGrid.Value.Owner;
        ent.Comp.CurrentShuttle = shuttleUid;
        ent.Comp.CurrentShuttlePrice = vessel.Price;
        ent.Comp.CurrentShuttleName = Loc.GetString(vessel.Name);
        Dirty(ent);

        TryDockPurchasedShuttle(shuttleUid, station);
        _audio.PlayPvs(ent.Comp.ConfirmSound, ent);
        _popup.PopupEntity(Loc.GetString("shipyard-console-purchase-success"), ent, args.Actor);
        Announce(ent, "shipyard-console-purchase-announcement",
            ("ship", Loc.GetString(vessel.Name)), ("cost", vessel.Price));
        UpdateUi(ent);
    }

    private void OnSell(Entity<ShipyardConsoleComponent> ent, ref ShipyardConsoleSellMessage args)
    {
        if (!_access.IsAllowed(args.Actor, ent))
        {
            Deny(ent, args.Actor, "shipyard-console-access-denied");
            return;
        }

        if (ent.Comp.CurrentShuttle is not { } shuttleUid || !Exists(shuttleUid))
        {
            ClearShuttle(ent);
            Deny(ent, args.Actor, "shipyard-console-no-shuttle");
            return;
        }

        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is not { } station ||
            !TryComp<StationBankAccountComponent>(station, out var bank))
        {
            Deny(ent, args.Actor, "shipyard-console-station-not-found");
            return;
        }

        if (!IsShuttleNearStation(shuttleUid, station, ent.Comp.MaxSellDistance))
        {
            Deny(ent, args.Actor, "shipyard-console-shuttle-too-far", ("distance", ent.Comp.MaxSellDistance));
            return;
        }

        if (IsShuttleOccupied(shuttleUid))
        {
            Deny(ent, args.Actor, "shipyard-console-shuttle-occupied");
            return;
        }

        var refund = (int) MathF.Round(ent.Comp.CurrentShuttlePrice * Math.Clamp(ent.Comp.SellRate, 0f, 1f));
        if (!_cargo.TryAdjustBankAccount((station, bank), ent.Comp.Account, refund))
        {
            Deny(ent, args.Actor, "shipyard-console-account-not-found");
            return;
        }

        var soldName = ent.Comp.CurrentShuttleName ?? Loc.GetString("shipyard-console-unknown-shuttle");
        QueueDel(shuttleUid);
        ClearShuttle(ent);
        _audio.PlayPvs(ent.Comp.ConfirmSound, ent);
        _popup.PopupEntity(Loc.GetString("shipyard-console-sell-success"), ent, args.Actor);
        Announce(ent, "shipyard-console-sale-announcement", ("ship", soldName), ("refund", refund));
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<ShipyardConsoleComponent> ent)
    {
        var accountName = Loc.GetString("shipyard-console-no-account");
        var balance = 0;
        var stationUid = _station.GetOwningStation(ent);
        if (stationUid is { } station &&
            TryComp<StationBankAccountComponent>(station, out var bank))
        {
            balance = _cargo.GetBalanceFromAccount((station, bank), ent.Comp.Account);
            if (_prototype.TryIndex<CargoAccountPrototype>(ent.Comp.Account, out var account))
                accountName = Loc.GetString(account.Name);
        }

        var currentSellValue = (int) MathF.Round(ent.Comp.CurrentShuttlePrice * Math.Clamp(ent.Comp.SellRate, 0f, 1f));
        if (ent.Comp.CurrentShuttle is not { } shuttle || !Exists(shuttle))
        {
            if (ent.Comp.CurrentShuttle is not null)
                ClearShuttle(ent);

            currentSellValue = 0;
        }

        var vessels = new List<ShipyardVesselData>();
        var added = new HashSet<string>();
        foreach (var vessel in _prototype.EnumeratePrototypes<ShipyardVesselPrototype>())
        {
            if (vessel.Group != ent.Comp.VesselGroup || !added.Add(vessel.ID))
                continue;

            vessels.Add(new ShipyardVesselData(
                vessel.ID,
                Loc.GetString(vessel.Name),
                Loc.GetString(vessel.Description),
                vessel.Price));
        }

        foreach (var vesselId in ent.Comp.Vessels)
        {
            if (!_prototype.TryIndex(vesselId, out ShipyardVesselPrototype? vessel) || !added.Add(vessel.ID))
                continue;

            vessels.Add(new ShipyardVesselData(
                vessel.ID,
                Loc.GetString(vessel.Name),
                Loc.GetString(vessel.Description),
                vessel.Price));
        }

        vessels.Sort((left, right) => string.Compare(left.Name, right.Name, StringComparison.CurrentCultureIgnoreCase));

        _ui.SetUiState(ent.Owner, ShipyardConsoleUiKey.Key, new ShipyardConsoleInterfaceState(
            accountName,
            balance,
            ent.Comp.CurrentShuttleName,
            ent.Comp.CurrentShuttlePrice,
            currentSellValue,
            Math.Clamp(ent.Comp.SellRate, 0f, 1f),
            vessels));
    }

    private bool TryGetVessel(ShipyardConsoleComponent component, string vesselId, out ShipyardVesselPrototype vessel)
    {
        vessel = default!;
        if (!_prototype.TryIndex<ShipyardVesselPrototype>(vesselId, out var candidate))
            return false;

        if (candidate.Group != component.VesselGroup && !component.Vessels.Contains(candidate.ID))
            return false;

        vessel = candidate;
        return true;
    }

    private bool TryDockPurchasedShuttle(EntityUid shuttleUid, EntityUid stationUid)
    {
        if (!TryComp<ShuttleComponent>(shuttleUid, out _) || !TryComp<StationDataComponent>(stationUid, out var stationData))
            return false;

        DockingConfig? bestConfig = null;
        foreach (var gridUid in stationData.Grids)
        {
            if (gridUid == shuttleUid || !Exists(gridUid))
                continue;

            var config = _docking.GetDockingConfig(shuttleUid, gridUid);
            if (config != null && (bestConfig == null || config.Docks.Count > bestConfig.Docks.Count))
                bestConfig = config;
        }

        if (bestConfig == null)
            return false;

        _shuttle.FTLDock((shuttleUid, Transform(shuttleUid)), bestConfig);
        return true;
    }

    private bool IsShuttleNearStation(EntityUid shuttleUid, EntityUid stationUid, float maxDistance)
    {
        if (!TryComp<StationDataComponent>(stationUid, out var stationData))
            return false;

        var shuttleTransform = Transform(shuttleUid);
        var shuttlePosition = _transform.GetWorldPosition(shuttleUid);
        var maxDistanceSquared = maxDistance * maxDistance;
        foreach (var gridUid in stationData.Grids)
        {
            if (gridUid == shuttleUid || !Exists(gridUid))
                continue;

            var gridTransform = Transform(gridUid);
            if (gridTransform.MapID != shuttleTransform.MapID)
                continue;

            if ((_transform.GetWorldPosition(gridUid) - shuttlePosition).LengthSquared() <= maxDistanceSquared)
                return true;
        }

        return false;
    }

    private bool IsShuttleOccupied(EntityUid shuttleUid)
    {
        var query = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (IsEntityOnShuttle(uid, shuttleUid, xform))
                return true;
        }

        return false;
    }

    private bool IsEntityOnShuttle(EntityUid uid, EntityUid shuttleUid, TransformComponent? xform = null)
    {
        if (!Resolve(uid, ref xform, false))
            return false;

        if (xform.GridUid == shuttleUid || xform.ParentUid == shuttleUid)
            return true;

        var parent = xform.ParentUid;
        while (parent.IsValid())
        {
            if (parent == shuttleUid)
                return true;

            if (!TryComp<TransformComponent>(parent, out var parentXform))
                return false;

            if (parentXform.GridUid == shuttleUid)
                return true;

            parent = parentXform.ParentUid;
        }

        return false;
    }

    private void ClearShuttle(Entity<ShipyardConsoleComponent> ent)
    {
        ent.Comp.CurrentShuttle = null;
        ent.Comp.CurrentShuttlePrice = 0;
        ent.Comp.CurrentShuttleName = null;
        Dirty(ent);
    }

    private void Deny(Entity<ShipyardConsoleComponent> ent, EntityUid user, string message, params (string Key, object Value)[] args)
    {
        _audio.PlayPvs(ent.Comp.ErrorSound, ent);
        _popup.PopupEntity(Loc.GetString(message, args), ent, user);
        UpdateUi(ent);
    }

    private void Announce(Entity<ShipyardConsoleComponent> ent, string message, params (string Key, object Value)[] args)
    {
        _radio.SendRadioMessage(ent, Loc.GetString(message, args), ent.Comp.AnnouncementChannel, ent, escapeMarkup: false);
    }
}
