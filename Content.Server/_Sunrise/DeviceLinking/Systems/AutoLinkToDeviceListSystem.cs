using Content.Server.DeviceLinking.Components;
using Content.Server.DeviceNetwork.Systems;
using Content.Server._Sunrise.DeviceLinking.Components;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Systems;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.DeviceLinking.Systems;

/// <summary>
/// Populates <see cref="DeviceListComponent"/> from autolink receivers on the same grid and channel.
/// </summary>
public sealed class AutoLinkToDeviceListSystem : EntitySystem
{
    [Dependency] private readonly DeviceListSystem _deviceList = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly TimeSpan DelayedMapInitLink = TimeSpan.FromSeconds(1);
    private readonly Dictionary<EntityUid, TimeSpan> _pendingMapInitLinks = new();
    private readonly List<EntityUid> _processed = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<AutoLinkToDeviceListComponent, MapInitEvent>(OnMapInit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingMapInitLinks.Count == 0)
            return;

        _processed.Clear();
        foreach (var (uid, triggerAt) in _pendingMapInitLinks)
        {
            if (_timing.CurTime < triggerAt)
                continue;

            _processed.Add(uid);
        }

        foreach (var uid in _processed)
        {
            _pendingMapInitLinks.Remove(uid);

            if (Deleted(uid))
                continue;

            if (!TryComp<AutoLinkToDeviceListComponent>(uid, out var autolink))
                continue;

            if (TryPopulate((uid, autolink)))
                RemCompDeferred<AutoLinkToDeviceListComponent>(uid);
        }
    }

    private void OnMapInit(Entity<AutoLinkToDeviceListComponent> ent, ref MapInitEvent args)
    {
        _pendingMapInitLinks[ent.Owner] = _timing.CurTime + DelayedMapInitLink;
    }

    private bool TryPopulate(Entity<AutoLinkToDeviceListComponent> ent)
    {
        if (!TryComp<AutoLinkTransmitterComponent>(ent, out var transmitter))
            return false;

        if (!TryComp<DeviceListComponent>(ent, out var deviceList))
            return false;

        if (!TryComp<TransformComponent>(ent, out var transmitterXform))
            return false;

        var transmitterGrid = transmitterXform.GridUid;
        if (transmitterGrid == null)
            return false;

        var matched = new List<EntityUid>();
        var query = EntityQueryEnumerator<AutoLinkReceiverComponent, DeviceNetworkComponent, TransformComponent>();

        while (query.MoveNext(out var receiverUid, out var receiver, out _, out var receiverXform))
        {
            if (receiverUid == ent.Owner)
                continue;

            if (receiver.AutoLinkChannel != transmitter.AutoLinkChannel)
                continue;

            if (receiverXform.GridUid != transmitterGrid)
                continue;

            matched.Add(receiverUid);
        }

        if (matched.Count == 0)
            return false;

        var result = _deviceList.UpdateDeviceList(ent.Owner, matched, ent.Comp.Merge, deviceList);
        return result == DeviceListUpdateResult.UpdateOk;
    }
}
