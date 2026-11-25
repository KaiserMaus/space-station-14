using Content.Server._Sunrise.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Shared.Objectives.Components;

namespace Content.Server._Sunrise.Objectives.Systems;

/// <summary>
/// Рассчитывает прогресс цели на кражу исследовательских данных.
/// </summary>
public sealed partial class StealResearchDataConditionSystem : EntitySystem
{
    [Dependency] private NumberObjectiveSystem _number = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StealResearchDataConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<StealResearchDataConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var target = _number.GetTarget(ent.Owner);
        args.Progress = target == 0
            ? 1f
            : MathF.Min(ent.Comp.DownloadedTechnologies.Count / (float) target, 1f);
    }
}
