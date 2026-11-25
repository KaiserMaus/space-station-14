using Content.Server._Sunrise.Objectives.Components;
using Content.Server._Sunrise.Research.Events;
using Content.Shared.Objectives.Components;

namespace Content.Server._Sunrise.Objectives.Systems;

/// <summary>
/// Завершает все выданные цели на перегрев при запуске перегрева любого полноценного сервера.
/// </summary>
public sealed partial class OverheatResearchServerConditionSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OverheatResearchServerConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
        SubscribeLocalEvent<ResearchServerOverheatStartedEvent>(OnOverheatStarted);
    }

    private void OnGetProgress(Entity<OverheatResearchServerConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        args.Progress = ent.Comp.Completed ? 1f : 0f;
    }

    private void OnOverheatStarted(ref ResearchServerOverheatStartedEvent args)
    {
        var query = EntityQueryEnumerator<OverheatResearchServerConditionComponent>();
        while (query.MoveNext(out _, out var condition))
        {
            condition.Completed = true;
        }
    }
}
