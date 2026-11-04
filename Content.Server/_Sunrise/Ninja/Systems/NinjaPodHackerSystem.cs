using Content.Shared._Sunrise.Ninja.Components;
using Content.Shared._Sunrise.Ninja.Events;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Ninja.Systems;

namespace Content.Server._Sunrise.Ninja.Systems;

/// <summary>
/// Handles the server-authoritative escape-pod extraction flow.
/// </summary>
public sealed partial class NinjaPodHackerSystem : EntitySystem
{
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedNinjaGlovesSystem _gloves = default!;
    [Dependency] private NinjaEscapePodExtractionSystem _extraction = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NinjaPodHackerComponent, BeforeInteractHandEvent>(OnBeforeInteractHand);
        SubscribeLocalEvent<NinjaPodHackerComponent, NinjaExtractionDoAfterEvent>(OnExtractionDoAfter);
    }

    private void OnBeforeInteractHand(Entity<NinjaPodHackerComponent> ent, ref BeforeInteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (TryStartExtraction(ent, args))
            args.Handled = true;
    }

    private void OnExtractionDoAfter(Entity<NinjaPodHackerComponent> ent, ref NinjaExtractionDoAfterEvent args)
    {
        if (args.Handled)
            return;

        if (TryCompleteExtraction(ent, args))
            args.Handled = true;
    }

    /// <summary>
    /// Tries to start hacking an escape-pod console.
    /// </summary>
    public bool TryStartExtraction(
        Entity<NinjaPodHackerComponent> ent,
        BeforeInteractHandEvent args)
    {
        if (!CanStartExtraction(ent, args, out var target))
            return false;

        var doAfterArgs = new DoAfterArgs(
            EntityManager,
            ent,
            ent.Comp.Delay,
            new NinjaExtractionDoAfterEvent(),
            ent,
            target,
            ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            CancelDuplicate = false,
        };

        return _doAfter.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    /// Returns whether the ninja can start hacking the selected console.
    /// </summary>
    public bool CanStartExtraction(
        Entity<NinjaPodHackerComponent> ent,
        BeforeInteractHandEvent args,
        out EntityUid target)
    {
        target = args.Target;

        if (!HasComp<PodConsoleComponent>(target))
            return false;

        return _gloves.AbilityCheck(ent, args, out target);
    }

    /// <summary>
    /// Tries to finish extraction after the console hack completes.
    /// </summary>
    public bool TryCompleteExtraction(
        Entity<NinjaPodHackerComponent> ent,
        NinjaExtractionDoAfterEvent args)
    {
        if (!CanCompleteExtraction(args) || args.Target is not { } target)
            return false;

        if (!_extraction.TryScheduleExtraction(ent, target, ent.Comp))
            return false;

        CompleteExtraction(ent);
        return true;
    }

    /// <summary>
    /// Returns whether the completed do-after still targets an escape-pod console.
    /// </summary>
    public bool CanCompleteExtraction(NinjaExtractionDoAfterEvent args)
    {
        if (args.Cancelled || args.Target is not { } target)
            return false;

        return HasComp<PodConsoleComponent>(target);
    }

    private void CompleteExtraction(Entity<NinjaPodHackerComponent> ent)
    {
        var objective = ent.Comp.Objective;

        RemComp<NinjaPodHackerComponent>(ent);

        var ev = new NinjaExtractedEvent(objective);
        RaiseLocalEvent(ent, ref ev);
    }
}
