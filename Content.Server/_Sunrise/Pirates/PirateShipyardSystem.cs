using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Prototypes;
using Content.Shared._Sunrise.Pirates.Components;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Pirates;

public sealed class PirateShipyardSystem : EntitySystem
{
    private static readonly ProtoId<CargoAccountPrototype> PirateAccount = "Pirates";

    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedCargoSystem _cargo = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PirateShipyardConsoleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PirateShipyardConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
    }

    private void OnInteractUsing(Entity<PirateShipyardConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp<PirateShuttleVoucherComponent>(args.Used, out var voucher))
            return;

        if (!TryComp<StationBankAccountComponent>(_station.GetOwningStation(ent) ?? EntityUid.Invalid, out _))
            return;

        var stationUid = _station.GetOwningStation(ent);
        if (stationUid == null || !TryComp<PirateStationComponent>(stationUid, out var pirateStation))
        {
            _popup.PopupEntity(Loc.GetString("pirate-shipyard-console-not-on-pirate-station"), ent, args.User);
            _audio.PlayPredicted(voucher.ErrorSound, ent, args.User);
            return;
        }

        if (pirateStation.CurrentShuttle is { Valid: true } existing && Exists(existing))
        {
            _popup.PopupEntity(Loc.GetString("pirate-shipyard-console-sell-first"), ent, args.User);
            _audio.PlayPredicted(voucher.ErrorSound, ent, args.User);
            return;
        }

        var mapId = Transform(ent).MapID;
        var coords = _transform.GetWorldPosition(ent) + voucher.Offset;

        if (!_mapLoader.TryLoadGrid(mapId, voucher.GridPath, out var shuttleGrid, offset: coords, rot: voucher.Rotation))
        {
            _popup.PopupEntity(Loc.GetString("pirate-shipyard-console-load-failed"), ent, args.User);
            _audio.PlayPredicted(voucher.ErrorSound, ent, args.User);
            return;
        }

        EnsureComp<PiratePurchasedShuttleComponent>(shuttleGrid.Value.Owner, out var purchased);
        purchased.Station = stationUid;
        purchased.PurchasePrice = voucher.Price;
        Dirty(shuttleGrid.Value.Owner, purchased);

        pirateStation.CurrentShuttle = shuttleGrid.Value.Owner;
        pirateStation.CurrentShuttleValue = purchased.PurchasePrice;
        Dirty(stationUid.Value, pirateStation);

        _popup.PopupEntity(Loc.GetString("pirate-shipyard-console-purchase-success"), ent, args.User);
        _audio.PlayPredicted(voucher.ConfirmSound, ent, args.User);
        QueueDel(args.Used);
        args.Handled = true;
    }

    private void OnGetAltVerbs(Entity<PirateShipyardConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var stationUid = _station.GetOwningStation(ent);
        if (stationUid == null || !TryComp<PirateStationComponent>(stationUid, out var pirateStation))
            return;

        if (pirateStation.CurrentShuttle is not { Valid: true } shuttleUid || !Exists(shuttleUid))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("pirate-shipyard-console-sell-verb"),
            Act = () => TrySellCurrentShuttle(ent, user, stationUid.Value, pirateStation, shuttleUid),
        });
    }

    private void TrySellCurrentShuttle(Entity<PirateShipyardConsoleComponent> ent, EntityUid user, EntityUid stationUid, PirateStationComponent pirateStation, EntityUid shuttleUid)
    {
        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (IsEntityOnShuttle(uid, shuttleUid, xform))
            {
                _popup.PopupEntity(Loc.GetString("pirate-shipyard-console-shuttle-occupied"), ent, user);
                _audio.PlayPredicted(ent.Comp.ErrorSound, ent, user);
                return;
            }
        }

        if (TryComp<StationBankAccountComponent>(stationUid, out var bank))
        {
            var refund = (int) MathF.Round(pirateStation.CurrentShuttleValue * ent.Comp.SellRate);
            if (refund > 0)
                _cargo.UpdateBankAccount((stationUid, bank), refund, PirateAccount);
        }

        QueueDel(shuttleUid);
        pirateStation.CurrentShuttle = null;
        pirateStation.CurrentShuttleValue = 0;
        Dirty(stationUid, pirateStation);

        _popup.PopupEntity(Loc.GetString("pirate-shipyard-console-sell-success"), ent, user);
        _audio.PlayPredicted(ent.Comp.ConfirmSound, ent, user);
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
                break;

            if (parentXform.GridUid == shuttleUid)
                return true;

            parent = parentXform.ParentUid;
        }

        return false;
    }
}
