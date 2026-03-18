using Content.Server.DeviceLinking.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.GameTicking.Events;
using Content.Server._Sunrise.DeviceLinking.Components;
using Content.Shared.DeviceNetwork.Components;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.DeviceLinking.Systems;

/// <summary>
/// Populates <see cref="DeviceListComponent"/> from autolink receivers on the same grid and channel.
/// </summary>
public sealed class AutoLinkToDeviceListSystem : EntitySystem
{
    [Dependency] private readonly DeviceListSystem _deviceList = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(3);
    private TimeSpan _nextRefresh = TimeSpan.Zero;

    public override void Initialize()
    {
        SubscribeLocalEvent<AutoLinkToDeviceListComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<AutoLinkToDeviceListComponent, ComponentStartup>(OnTransmitterStartup);
        SubscribeLocalEvent<AutoLinkReceiverComponent, MapInitEvent>(OnReceiverMapInit);
        SubscribeLocalEvent<AutoLinkReceiverComponent, ComponentStartup>(OnReceiverStartup);
        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;

        var query = EntityQueryEnumerator<AutoLinkToDeviceListComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            TryPopulate((uid, comp));
        }
    }

    private void OnMapInit(Entity<AutoLinkToDeviceListComponent> ent, ref MapInitEvent args)
    {
        TryPopulate(ent);
    }

    private void OnRoundStarting(RoundStartingEvent ev)
    {
        var query = EntityQueryEnumerator<AutoLinkToDeviceListComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            TryPopulate((uid, comp));
        }
    }

    private void OnReceiverMapInit(Entity<AutoLinkReceiverComponent> ent, ref MapInitEvent args)
    {
        RefreshTransmitters(ent.Comp.AutoLinkChannel);
    }

    private void OnTransmitterStartup(Entity<AutoLinkToDeviceListComponent> ent, ref ComponentStartup args)
    {
        TryPopulate(ent);
    }

    private void OnReceiverStartup(Entity<AutoLinkReceiverComponent> ent, ref ComponentStartup args)
    {
        RefreshTransmitters(ent.Comp.AutoLinkChannel);
    }

    private void RefreshTransmitters(string channel)
    {
        var query = EntityQueryEnumerator<AutoLinkToDeviceListComponent, AutoLinkTransmitterComponent>();
        while (query.MoveNext(out var uid, out var autolink, out var transmitter))
        {
            if (transmitter.AutoLinkChannel != channel)
                continue;

            TryPopulate((uid, autolink));
        }
    }

    private void TryPopulate(Entity<AutoLinkToDeviceListComponent> ent)
    {
        if (!TryComp<AutoLinkTransmitterComponent>(ent, out var transmitter))
            return;

        if (!TryComp<DeviceListComponent>(ent, out var deviceList))
            return;

        var matched = new List<EntityUid>();
        var query = EntityQueryEnumerator<AutoLinkReceiverComponent, DeviceNetworkComponent, TransformComponent>();

        while (query.MoveNext(out var receiverUid, out var receiver, out _, out _))
        {
            if (receiverUid == ent.Owner)
                continue;

            if (receiver.AutoLinkChannel != transmitter.AutoLinkChannel)
                continue;

            matched.Add(receiverUid);
        }

        if (matched.Count == 0)
            return;

        _deviceList.UpdateDeviceList(ent.Owner, matched, ent.Comp.Merge, deviceList);
    }
}
