using Content.Server._Sunrise.Objectives.Systems;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Objectives.Components;

/// <summary>
/// Хранит уникальные технологии, скачанные для цели на саботаж исследований.
/// </summary>
[RegisterComponent, Access(typeof(StealResearchDataConditionSystem))]
public sealed partial class StealResearchDataConditionComponent : Component
{
    [DataField]
    public HashSet<ProtoId<TechnologyPrototype>> DownloadedTechnologies = [];

    [DataField]
    public bool AnnouncementSent;
}
