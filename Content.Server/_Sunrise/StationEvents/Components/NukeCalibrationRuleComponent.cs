using Content.Server.StationEvents.Events;
using Robust.Shared.Audio;

namespace Content.Server.StationEvents.Components;

[RegisterComponent, Access(typeof(NukeCalibrationRule))]
public sealed partial class NukeCalibrationRuleComponent : Component
{
    /// <summary>
    ///     Sound played if automatic disarm fails.
    /// </summary>
    [DataField]
    public SoundSpecifier AutoDisarmFailedSound = new SoundPathSpecifier("/Audio/Misc/notice1.ogg");

    /// <summary>
    ///     Sound played if automatic disarm succeeds.
    /// </summary>
    [DataField]
    public SoundSpecifier AutoDisarmSuccessSound = new SoundPathSpecifier("/Audio/Misc/notice2.ogg");

    [DataField]
    public EntityUid AffectedStation;

    /// <summary>
    ///     Nuke affected by calibration.
    /// </summary>
    [DataField]
    public EntityUid AffectedNuke;

    [DataField]
    public float NukeTimer = 170f;

    [DataField]
    public float AutoDisarmChance = 0.5f;

    [DataField]
    public float TimeUntilFirstAnnouncement = 15f;

    [DataField]
    public bool FirstAnnouncementMade = false;
}
