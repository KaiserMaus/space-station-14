using Content.Server._Sunrise.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Research.Components;
using Content.Shared.Station.Components;

namespace Content.Server._Sunrise.Objectives.Systems;

/// <summary>
/// Проверяет наличие подходящего исследовательского сервера на любом гриде станции.
/// </summary>
public sealed partial class ResearchServerObjectiveRequirementSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ResearchServerObjectiveRequirementComponent, RequirementCheckEvent>(OnCheck);
    }

    private void OnCheck(Entity<ResearchServerObjectiveRequirementComponent> ent, ref RequirementCheckEvent args)
    {
        if (args.Cancelled || HasEligibleServer(ent.Comp.AllowLimitedServers))
            return;

        args.Cancelled = true;
    }

    private bool HasEligibleServer(bool allowLimitedServers)
    {
        var query = EntityQueryEnumerator<ResearchServerComponent, TechnologyDatabaseComponent, TransformComponent>();
        while (query.MoveNext(out _, out _, out var database, out var transform))
        {
            if (transform.GridUid is not { } grid || !HasComp<StationMemberComponent>(grid))
                continue;

            if (allowLimitedServers || database.MaximumTechnologyTier == null)
                return true;
        }

        return false;
    }
}
