using Content.Server._Sunrise.TransitHub;
using Content.Server.Shuttles.Components;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Map.Components;

#pragma warning disable IDE0130 // Пространство имён должно совпадать с существующей partial-системой.
namespace Content.Server.Shuttles.Systems;

public sealed partial class EmergencyShuttleSystem
{
    /// <summary>
    /// Launches one validated escape pod towards the station transit hub.
    /// </summary>
    public bool TryLaunchEscapePod(EntityUid podUid, float hyperspaceTime)
    {
        if (!TryComp<EscapePodComponent>(podUid, out _) ||
            !TryComp<ShuttleComponent>(podUid, out var shuttle) ||
            HasComp<FTLComponent>(podUid))
        {
            return false;
        }

        var station = _station.GetOwningStation(podUid);
        if (station is not { } stationUid ||
            !TryComp<StationTransitHubComponent>(stationUid, out var transitHub) ||
            transitHub.Entity is not { } destination ||
            Deleted(destination) ||
            !HasComp<MapGridComponent>(destination))
        {
            return false;
        }

        _shuttle.FTLToDock(podUid, shuttle, destination, startupTime: 0f, hyperspaceTime: hyperspaceTime);
        if (!HasComp<FTLComponent>(podUid))
            return false;

        RemComp<EscapePodComponent>(podUid);
        return true;
    }
}
