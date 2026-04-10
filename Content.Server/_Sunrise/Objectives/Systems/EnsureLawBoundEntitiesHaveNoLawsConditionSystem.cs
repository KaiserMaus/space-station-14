using Content.Server.Silicons.Laws;
using Content.Server._Sunrise.Objectives.Components;
using Content.Shared.Objectives.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Whitelist;

namespace Content.Server._Sunrise.Objectives.Systems;

public sealed class EnsureLawBoundEntitiesHaveNoLawsConditionSystem : EntitySystem
{
    [Dependency] private readonly SiliconLawSystem _siliconLaw = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EnsureLawBoundEntitiesHaveNoLawsConditionComponent, ObjectiveGetProgressEvent>(OnGetProgress);
    }

    private void OnGetProgress(Entity<EnsureLawBoundEntitiesHaveNoLawsConditionComponent> ent, ref ObjectiveGetProgressEvent args)
    {
        var query = EntityQueryEnumerator<SiliconLawBoundComponent>();
        var freeEntities = 0;

        while (query.MoveNext(out var lawBoundUid, out var lawBound))
        {
            if (!_whitelist.CheckBoth(lawBoundUid, ent.Comp.LawEntityBlacklist, ent.Comp.LawEntityWhitelist))
                continue;

            var laws = _siliconLaw.GetLaws(lawBoundUid, lawBound);
            if (laws.Laws.Count == 0)
                freeEntities++;
        }

        if (ent.Comp.EntitiesToFree <= 0)
        {
            args.Progress = 1f;
            return;
        }

        args.Progress = Math.Clamp(freeEntities / (float) ent.Comp.EntitiesToFree, 0f, 1f);
    }
}
