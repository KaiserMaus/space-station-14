using Robust.Shared.Audio;

namespace Content.Shared._Sunrise.Implants.Components;

/// <summary>
/// Data for mind control implant behavior.
/// </summary>
[RegisterComponent]
public sealed partial class MindControlImplantComponent : Component
{
    [DataField]
    public EntityUid Master;

    [DataField]
    public LocId BriefingText = "mind-control-user-briefing";

    [DataField]
    public LocId DebriefingText = "mind-control-user-freed";

    [DataField]
    public SoundSpecifier BriefingSound = new SoundPathSpecifier("/Audio/Ambience/Antag/traitor_start.ogg");
}

