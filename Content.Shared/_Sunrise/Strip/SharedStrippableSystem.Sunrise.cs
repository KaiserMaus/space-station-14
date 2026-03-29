using System.Linq;
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
    private readonly Dictionary<EntityUid, Queue<DoAfterId>> _activeStripDoAfters = new();

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
            puller.NeedsHands &&
            puller.Pulling != null)
            freeHands--;

        return Math.Max(0, freeHands);
    }

    private void LimitSimultaneousStripDoAfters(Entity<HandsComponent?> user, DoAfterArgs doAfterArgs)
    {
        var userId = user.Owner;
        var freeHands = GetAvailableStripHands(user);

        if (freeHands == 0)
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-no-hands-available"));
            return;
        }

        if (!_activeStripDoAfters.TryGetValue(userId, out var queue))
        {
            queue = new Queue<DoAfterId>();
            _activeStripDoAfters[userId] = queue;
        }

        while (queue.Count >= freeHands && queue.Count > 0)
        {
            var oldest = queue.Dequeue();
            _doAfterSystem.Cancel(oldest);
        }

        if (_doAfterSystem.TryStartDoAfter(doAfterArgs, out var doAfterId) && doAfterId != null)
            queue.Enqueue(doAfterId.Value);
    }

    private void RevalidateSimultaneousStripDoAfter(
        Entity<HandsComponent> entity,
        ref DoAfterAttemptEvent<StrippableDoAfterEvent> ev)
    {
        var freeHands = GetAvailableStripHands((entity.Owner, entity.Comp));

        if (freeHands == 0)
        {
            ev.Cancel();
            return;
        }

        if (!_activeStripDoAfters.TryGetValue(entity.Owner, out var queue))
            return;

        var excess = queue.Count - freeHands;
        if (excess <= 0)
            return;

        var queueList = queue.ToList();
        var newestExcess = queueList.GetRange(queueList.Count - excess, excess);
        if (newestExcess.Contains(ev.DoAfter.Id))
            ev.Cancel();
    }

    private void CleanupTrackedStripDoAfter(EntityUid user, DoAfterId doAfterId)
    {
        if (!_activeStripDoAfters.TryGetValue(user, out var queue))
            return;

        var newQueue = new Queue<DoAfterId>(queue.Count);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (id != doAfterId)
                newQueue.Enqueue(id);
        }

        _activeStripDoAfters[user] = newQueue;
    }

    private void OnBeforeGettingStripped(EntityUid uid, StrippableComponent component, ref BeforeGettingStrippedEvent ev)
    {
        if (!TryComp<CuffableComponent>(uid, out var cuffable))
            return;

        if (_cuffableSystem.IsCuffed((uid, cuffable)))
            ev.Multiplier *= 0.5f;
    }
}
