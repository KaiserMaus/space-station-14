using Content.Server._Sunrise.Ninja.Components;
using Content.Server.Chat.Systems;
using Content.Server.Pinpointer;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.Shuttles.Systems;
using Content.Shared._Sunrise.Ninja.Components;
using Content.Shared.Chat;
using Content.Shared.Shuttles.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.Ninja.Systems;

/// <summary>
/// Controls the launch and in-flight timeline of a ninja escape pod.
/// </summary>
public sealed partial class NinjaEscapePodExtractionSystem : EntitySystem
{
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private EmergencyShuttleSystem _emergencyShuttle = default!;
    [Dependency] private NavMapSystem _navMap = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaEscapePodExtractionComponent, FTLStartedEvent>(OnFtlStarted);
    }

    /// <summary>
    /// Checks whether this console can schedule a new extraction.
    /// </summary>
    public bool CanScheduleExtraction(EntityUid ninja, EntityUid console, NinjaPodHackerComponent hacker)
    {
        if (Deleted(ninja) || Deleted(console))
            return false;

        var grid = Transform(console).GridUid;
        return grid is { } gridUid &&
               HasComp<EscapePodComponent>(gridUid) &&
               HasComp<ShuttleComponent>(gridUid) &&
               !HasComp<NinjaEscapePodExtractionComponent>(gridUid);
    }

    /// <summary>
    /// Schedules extraction and announces the compromised pod to its station.
    /// </summary>
    public bool TryScheduleExtraction(EntityUid ninja, EntityUid console, NinjaPodHackerComponent hacker)
    {
        if (!CanScheduleExtraction(ninja, console, hacker))
            return false;

        var grid = Transform(console).GridUid!.Value;
        var extraction = new NinjaEscapePodExtractionComponent();
        AddComp(grid, extraction);
        extraction.Ninja = ninja;
        extraction.Console = console;
        extraction.LaunchAt = _timing.CurTime + hacker.LaunchDelay;
        extraction.FtlTravelTime = hacker.FtlTravelTime;
        extraction.MessageDelay = hacker.MessageDelay;
        extraction.NinjaRemovalDelay = hacker.NinjaRemovalDelay;
        extraction.PodRemovalDelay = hacker.PodRemovalDelay;

        var location = FormattedMessage.RemoveMarkupOrThrow(
            _navMap.GetNearestBeaconString((console, Transform(console)), onlyName: true));
        var message = Loc.GetString("ninja-extraction-station-announcement", ("location", location));
        var sender = Loc.GetString("ninja-extraction-station-ai-sender");
        _chat.DispatchStationAnnouncement(console, message, sender, playTts: true);

        return true;
    }

    private void OnFtlStarted(
        Entity<NinjaEscapePodExtractionComponent> ent,
        ref FTLStartedEvent args)
    {
        if (ent.Comp.FtlStartedAt != null)
            return;

        var startedAt = _timing.CurTime;
        ent.Comp.FtlStartedAt = startedAt;
        ent.Comp.MessageAt = startedAt + ent.Comp.MessageDelay;
        ent.Comp.NinjaRemovalAt = startedAt + ent.Comp.NinjaRemovalDelay;
        ent.Comp.PodRemovalAt = startedAt + ent.Comp.PodRemovalDelay;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<NinjaEscapePodExtractionComponent>();
        while (query.MoveNext(out var uid, out var extraction))
        {
            if (extraction.FtlStartedAt == null)
            {
                UpdatePendingLaunch(uid, extraction);
                continue;
            }

            UpdateFlight(uid, extraction);
        }
    }

    private void UpdatePendingLaunch(EntityUid uid, NinjaEscapePodExtractionComponent extraction)
    {
        if (extraction.LaunchAttempted)
            return;

        if (Deleted(extraction.Ninja) || Deleted(extraction.Console))
        {
            RemCompDeferred<NinjaEscapePodExtractionComponent>(uid);
            return;
        }

        if (extraction.LaunchAt > _timing.CurTime)
            return;

        extraction.LaunchAttempted = true;
        if (!_emergencyShuttle.TryLaunchEscapePod(uid, extraction.FtlTravelTime))
            RemCompDeferred<NinjaEscapePodExtractionComponent>(uid);
    }

    private void UpdateFlight(EntityUid uid, NinjaEscapePodExtractionComponent extraction)
    {
        var currentTime = _timing.CurTime;

        if (!extraction.MessageSent && extraction.MessageAt <= currentTime)
        {
            if (!Deleted(extraction.Ninja))
            {
                if (!Deleted(extraction.Console))
                {
                    var spokenMessage = Loc.GetString("ninja-extraction-console-speech",
                        ("target", Name(extraction.Ninja)));
                    _chat.TrySendInGameICMessage(
                        extraction.Console,
                        spokenMessage,
                        InGameICChatType.Speak,
                        hideChat: false);
                }

            }

            extraction.MessageSent = true;
        }

        if (!extraction.NinjaRemoved && extraction.NinjaRemovalAt <= currentTime)
        {
            extraction.NinjaRemoved = true;
            if (!Deleted(extraction.Ninja))
            {
                Spawn("RedspaceImplantEffect", Transform(extraction.Ninja).Coordinates);
                QueueDel(extraction.Ninja);
            }
        }

        if (extraction.PodRemovalAt <= currentTime)
            QueueDel(uid);
    }
}
