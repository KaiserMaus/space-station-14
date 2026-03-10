using System.Linq;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Strip.Components;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Shared.Strip;
#pragma warning restore IDE0130 // Namespace does not match folder structure

public abstract partial class SharedStrippableSystem
{
    [Dependency] private readonly PullingSystem _pullingSystem = default!;

    private readonly Dictionary<EntityUid, Queue<DoAfterId>> _activeStripDoAfters = new();

    partial void InitializeSunrise()
    {
        SubscribeLocalEvent<StrippableComponent, BeforeGettingStrippedEvent>(OnBeforeGettingStripped);
    }

    private partial bool CanUseStrippingDragDrop(EntityUid target)
    {
        return TryComp<StrippingComponent>(target, out var strippingComp) && strippingComp.UseDragDrop;
    }

    private int GetAvailableStripHands(Entity<HandsComponent?> user)
    {
        if (!TryComp<HandsComponent>(user.Owner, out var handsComp))
            return 0;

        var handsHolding = 0;
        foreach (var hand in handsComp.Hands.Keys)
        {
            if (_handsSystem.GetHeldItem(user, hand) != null)
                handsHolding++;
        }

        var freeHands = handsComp.Count - handsHolding;
        if (_pullingSystem.IsPulling(user.Owner))
            freeHands--;

        return Math.Max(0, freeHands);
    }

    private partial void LimitSimultaneousStripDoAfters(Entity<HandsComponent?> user, DoAfterArgs doAfterArgs)
    {
        var userId = user.Owner;
        var freeHands = GetAvailableStripHands(user);

        if (freeHands == 0)
        {
            if (doAfterArgs.Event is not StrippableDoAfterEvent strippableEvent || !strippableEvent.InsertOrRemove)
            {
                _popupSystem.PopupCursor(Loc.GetString("strippable-component-no-hands-available"));
                return;
            }
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

    private partial void RevalidateSimultaneousStripDoAfter(
        Entity<HandsComponent> entity,
        ref DoAfterAttemptEvent<StrippableDoAfterEvent> ev)
    {
        var freeHands = GetAvailableStripHands((entity.Owner, entity.Comp));

        if (freeHands == 0)
        {
            if (!ev.Event.InsertOrRemove)
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

    private partial void CleanupTrackedStripDoAfter(EntityUid user, DoAfterId doAfterId)
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

        var entity = new Entity<CuffableComponent>(uid, cuffable);
        if (_cuffableSystem.IsCuffed(entity))
            ev.Multiplier *= 0.5f;
    }
}
