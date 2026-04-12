using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Strip.Components;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Strip;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public abstract partial class SharedStrippableSystem
{
    private void InitializeSunrise()
    {
        SubscribeLocalEvent<StrippableComponent, BeforeGettingStrippedEvent>(OnBeforeGettingStripped);
    }

    private bool CanUseStrippingDragDrop(EntityUid target)
    {
        return TryComp<StrippingComponent>(target, out var strippingComp) && strippingComp.UseDragDrop;
    }

    private int GetAvailableStripHands(Entity<HandsComponent?> user)
    {
        var freeHands = _handsSystem.CountFreeHands(user);

        if (TryComp<PullerComponent>(user.Owner, out var puller) &&
            puller.Pulling != null &&
            puller.NeedsHands)
        {
            freeHands = Math.Max(0, freeHands - 1);
        }

        var totalHands = _handsSystem.GetHandCount(user);
        return Math.Min(freeHands, totalHands);
    }

    private int CountActiveStripDoAfters(DoAfterComponent doAfterComp)
    {
        var active = 0;

        foreach (var doAfter in doAfterComp.DoAfters.Values)
        {
            if (doAfter.Cancelled || doAfter.Completed)
                continue;

            if (doAfter.Args.Event is not StrippableDoAfterEvent)
                continue;

            active++;
        }

        return active;
    }

    private void LimitSimultaneousStripDoAfters(Entity<HandsComponent?> user, DoAfterArgs doAfterArgs)
    {
        var availableHands = GetAvailableStripHands(user);

        if (availableHands <= 0)
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-no-hands-available"));
            return;
        }

        var activeStripDoAfters = 0;
        if (TryComp<DoAfterComponent>(user.Owner, out var doAfterComp))
        {
            activeStripDoAfters = CountActiveStripDoAfters(doAfterComp);
        }

        if (activeStripDoAfters >= availableHands)
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-no-hands-available"));
            return;
        }

        _doAfterSystem.TryStartDoAfter(doAfterArgs);
    }

    private void RevalidateSimultaneousStripDoAfter(
        Entity<HandsComponent> entity,
        ref DoAfterAttemptEvent<StrippableDoAfterEvent> ev)
    {
        var availableHands = GetAvailableStripHands((entity.Owner, entity.Comp));

        if (availableHands <= 0)
        {
            ev.Cancel();
            return;
        }

        if (!TryComp<DoAfterComponent>(entity.Owner, out var doAfterComp))
            return;

        var activeStripDoAfters = CountActiveStripDoAfters(doAfterComp);
        if (activeStripDoAfters > availableHands)
            ev.Cancel();
    }

    private void OnBeforeGettingStripped(EntityUid uid, StrippableComponent component, ref BeforeGettingStrippedEvent ev)
    {
        if (!TryComp<CuffableComponent>(uid, out var cuffable))
            return;

        if (_cuffableSystem.IsCuffed((uid, cuffable)))
            ev.Multiplier *= 0.5f;
    }
}
