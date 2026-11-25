using Content.Shared.Research.Components;
using Content.Shared.Research.Systems;
using Robust.Shared.Random;

namespace Content.Server.Research.Systems;

public sealed partial class ResearchStealerSystem : SharedResearchStealerSystem
{
    [Dependency] private SharedResearchSystem _research = default!;
    [Dependency] private IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ResearchStealerComponent, ResearchStealDoAfterEvent>(OnDoAfter);
    }

    private void OnDoAfter(EntityUid uid, ResearchStealerComponent comp, ResearchStealDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null)
            return;

        var target = args.Target.Value;

        if (!TryComp<TechnologyDatabaseComponent>(target, out var database))
            return;

        // Sunrise added start - Кража сохраняет историю исследования и пропускает уже украденные и финальные технологии.
        var stealable = _research.GetStealableTechnologies(database);
        var ev = new ResearchStolenEvent(uid, target, new());
        var count = _random.Next(comp.MinToSteal, comp.MaxToSteal + 1);
        for (var i = 0; i < count && stealable.Count > 0; i++)
        {
            var technology = _random.PickAndTake(stealable);
            if (_research.TryStealTechnology((target, database), technology.ID))
                ev.Techs.Add(technology.ID);
        }
        // Sunrise added end
        RaiseLocalEvent(uid, ref ev);

        args.Handled = true;
    }
}

/// <summary>
/// Event raised on the user when research is stolen from a RND server.
/// Techs contains every technology id researched.
/// </summary>
[ByRefEvent]
public record struct ResearchStolenEvent(EntityUid Used, EntityUid Target, List<string> Techs);
