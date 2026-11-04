using Content.Server.Objectives.Systems;
using Content.Shared._Sunrise.Ninja.Events;
using Content.Shared.Ninja.Components;

namespace Content.Server._Sunrise.Ninja.Systems;

/// <summary>
/// Completes a ninja's configured extraction objective.
/// </summary>
public sealed partial class NinjaExtractionObjectiveSystem : EntitySystem
{
    [Dependency] private CodeConditionSystem _codeCondition = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpaceNinjaComponent, NinjaExtractedEvent>(OnNinjaExtracted);
    }

    private void OnNinjaExtracted(Entity<SpaceNinjaComponent> ent, ref NinjaExtractedEvent args)
    {
        _codeCondition.SetCompleted(ent.Owner, args.Objective);
    }
}
