using Content.Server.Popups;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Pirate;
using Content.Shared.Cargo;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Pirates;

public sealed class PirateShipyardSystem : EntitySystem
{
    private static readonly ProtoId<CargoAccountPrototype> PirateAccount = "Pirates";

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCargoSystem _cargo = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PirateShipyardConsoleComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<PirateShipyardConsoleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
    }

    private void OnInteractUsing(Entity<PirateShipyardConsoleComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (!TryComp<PirateShuttleVoucherComponent>(args.Used, out var voucher))
            return;

        if (!TryRedeemVoucher(ent, args.User, args.Used, voucher))
            return;

        args.Handled = true;
    }

    private void OnGetAlternativeVerbs(Entity<PirateShipyardConsoleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!TryGetPirateStation(ent.Owner, out var stationUid, out var pirateStation))
            return;

        if (pirateStation.CurrentShuttle is not { Valid: true } shuttleUid || !Exists(shuttleUid))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("pirate-shipyard-console-sell-verb"),
            Act = () => TrySellCurrentShuttle(ent, user, stationUid, pirateStation, shuttleUid),
        });
    }

    public bool TryRedeemVoucher(
        Entity<PirateShipyardConsoleComponent> console,
        EntityUid user,
        EntityUid voucherUid,
        PirateShuttleVoucherComponent voucher)
    {
        if (!TryGetPirateStation(console.Owner, out var stationUid, out var pirateStation))
        {
            ShowError(console, user, voucher.ErrorSound, "pirate-shipyard-console-not-on-pirate-station");
            return false;
        }

        if (pirateStation.CurrentShuttle is { Valid: true } existing && Exists(existing))
        {
            ShowError(console, user, voucher.ErrorSound, "pirate-shipyard-console-sell-first");
            return false;
        }

        var mapId = Transform(console.Owner).MapID;
        var coords = _transform.GetWorldPosition(console.Owner) + voucher.Offset;
        if (!_mapLoader.TryLoadGrid(mapId, voucher.GridPath, out var shuttleGrid, offset: coords, rot: voucher.Rotation))
        {
            ShowError(console, user, voucher.ErrorSound, "pirate-shipyard-console-load-failed");
            return false;
        }

        var shuttleUid = shuttleGrid.Value.Owner;
        EnsureComp<PiratePurchasedShuttleComponent>(shuttleUid, out var purchased);
        purchased.Station = stationUid;
        purchased.PurchasePrice = voucher.Price;
        Dirty(shuttleUid, purchased);

        pirateStation.CurrentShuttle = shuttleUid;
        pirateStation.CurrentShuttleValue = purchased.PurchasePrice;
        Dirty(stationUid, pirateStation);

        _popup.PopupEntity(Loc.GetString("pirate-shipyard-console-purchase-success"), console.Owner, user);
        _audio.PlayPredicted(voucher.ConfirmSound, console.Owner, user);
        QueueDel(voucherUid);
        return true;
    }

    public bool TrySellCurrentShuttle(
        Entity<PirateShipyardConsoleComponent> console,
        EntityUid user,
        EntityUid stationUid,
        PirateStationComponent pirateStation,
        EntityUid shuttleUid)
    {
        var actorQuery = EntityQueryEnumerator<ActorComponent, TransformComponent>();
        while (actorQuery.MoveNext(out var uid, out _, out var xform))
        {
            if (!IsEntityOnShuttle(uid, shuttleUid, xform))
                continue;

            ShowError(console, user, console.Comp.ErrorSound, "pirate-shipyard-console-shuttle-occupied");
            return false;
        }

        if (TryComp<StationBankAccountComponent>(stationUid, out var bank))
        {
            var refund = (int) MathF.Round(pirateStation.CurrentShuttleValue * console.Comp.SellRate);
            if (refund > 0)
                _cargo.UpdateBankAccount((stationUid, bank), refund, PirateAccount);
        }

        QueueDel(shuttleUid);
        pirateStation.CurrentShuttle = null;
        pirateStation.CurrentShuttleValue = 0;
        Dirty(stationUid, pirateStation);

        _popup.PopupEntity(Loc.GetString("pirate-shipyard-console-sell-success"), console.Owner, user);
        _audio.PlayPredicted(console.Comp.ConfirmSound, console.Owner, user);
        return true;
    }

    private bool TryGetPirateStation(EntityUid consoleUid, out EntityUid stationUid, out PirateStationComponent pirateStation)
    {
        stationUid = _station.GetOwningStation(consoleUid) ?? EntityUid.Invalid;
        if (stationUid == EntityUid.Invalid || !TryComp<PirateStationComponent>(stationUid, out var component))
        {
            pirateStation = default!;
            return false;
        }

        pirateStation = component;
        return true;
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

    private void ShowError(
        Entity<PirateShipyardConsoleComponent> console,
        EntityUid user,
        SoundSpecifier sound,
        string locId)
    {
        _popup.PopupEntity(Loc.GetString(locId), console.Owner, user);
        _audio.PlayPredicted(sound, console.Owner, user);
    }
}
