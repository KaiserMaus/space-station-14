using Content.Server.Chat.Systems;
using Content.Server.Nuke;
using Content.Server.Popups;
using Content.Server.StationEvents.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking.Components;
using Content.Shared.Nuke;
using Content.Shared.Popups;
using Content.Shared.Station.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server.StationEvents.Events;

public sealed class NukeCalibrationRule : StationEventSystem<NukeCalibrationRuleComponent>
{
    [Dependency] private readonly NukeSystem _nukeSystem = default!;
    [Dependency] private readonly ChatSystem _chatSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly PopupSystem _popups = default!;
    [Dependency] private readonly TransformSystem _transform = default!;

    protected override void Started(EntityUid uid, NukeCalibrationRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var affectedStation))
            return;

        component.AffectedStation = affectedStation.Value;

        var nukeQuery = AllEntityQuery<NukeComponent, TransformComponent>();
        while (nukeQuery.MoveNext(out var nuke, out var nukeComponent, out var nukeTransform))
        {
            // Don't calibrate a nuke outside of the selected station.
            if (CompOrNull<StationMemberComponent>(nukeTransform.GridUid)?.Station != affectedStation)
                continue;

            if (nukeComponent.Status == NukeStatus.ARMED)
                continue;

            // Anchor if possible, otherwise skip this nuke.
            if (!nukeTransform.Anchored && !_transform.AnchorEntity(nuke, nukeTransform))
                continue;

            _nukeSystem.SetRemainingTime(nuke, component.NukeTimer);
            _nukeSystem.ArmBomb(nuke, nukeComponent);
            component.AffectedNuke = nuke;

            if (!nukeComponent.DiskSlot.HasItem)
            {
                _popups.PopupEntity(Loc.GetString("station-event-nuke-calibration-arm-popup"), nuke, PopupType.LargeCaution);
            }
            else
            {
                // Force eject because armed nuke keeps the slot locked.
                if (_itemSlots.TryEject(nuke, nukeComponent.DiskSlot, user: null, out _, true))
                    _popups.PopupEntity(Loc.GetString("station-event-nuke-calibration-arm-and-disk-ejected-popup"), nuke, PopupType.LargeCaution);
                else
                    _popups.PopupEntity(Loc.GetString("station-event-nuke-calibration-arm-popup"), nuke, PopupType.LargeCaution);
            }

            break;
        }
    }

    protected override void Ended(EntityUid uid, NukeCalibrationRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        if (!TryComp<NukeComponent>(component.AffectedNuke, out var nukeComp))
            return;

        // Lucky path: nuke gets auto-disarmed.
        if (RobustRandom.NextFloat() <= component.AutoDisarmChance)
        {
            if (nukeComp.Status != NukeStatus.ARMED)
                return;

            _nukeSystem.SetRemainingTime(component.AffectedNuke, nukeComp.Timer); // Sunrise-Edit
            _nukeSystem.DisarmBomb(component.AffectedNuke, nukeComp);
            _chatSystem.DispatchGlobalAnnouncement(
                Loc.GetString("station-event-nuke-calibration-disarm-success-announcement"),
                playDefault: false,
                colorOverride: Color.Green);
            _audioSystem.PlayGlobal(component.AutoDisarmSuccessSound, Filter.Broadcast(), true);
            return;
        }

        if (nukeComp.Status != NukeStatus.ARMED)
            return;

        // Failed auto-disarm: allow manual disarm without the disk.
        _nukeSystem.SetDiskBypassEnabled(component.AffectedNuke, true, true, nukeComp);
        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString("station-event-nuke-calibration-disarm-fail-announcement"),
            playDefault: false,
            colorOverride: Color.Crimson);
        _audioSystem.PlayGlobal(component.AutoDisarmFailedSound, Filter.Broadcast(), true);
    }

    protected override void ActiveTick(EntityUid uid, NukeCalibrationRuleComponent component, GameRuleComponent gameRule, float frameTime)
    {
        base.ActiveTick(uid, component, gameRule, frameTime);

        if (component.FirstAnnouncementMade)
            return;

        component.TimeUntilFirstAnnouncement -= frameTime;
        if (component.TimeUntilFirstAnnouncement > 0f)
            return;

        component.FirstAnnouncementMade = true;
        _chatSystem.DispatchGlobalAnnouncement(
            Loc.GetString("station-event-nuke-calibration-midway-announcement"),
            colorOverride: Color.Yellow);
    }
}
