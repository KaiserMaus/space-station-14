using System.Linq;
using Content.Shared.Administration.Logs;
using Content.Shared.CombatMode;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Movement.Pulling.Systems; // Sunrise-Add
using Content.Shared.Popups;
using Content.Shared.Strip.Components;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Shared.Strip;

public abstract class SharedStrippableSystem : EntitySystem
{
    [Dependency] private readonly SharedInteractionSystem _interactionSystem = default!;

    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    [Dependency] private readonly InventorySystem _inventorySystem = default!;

    [Dependency] private readonly SharedCuffableSystem _cuffableSystem = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfterSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popupSystem = default!;
    [Dependency] private readonly PullingSystem _pullingSystem = default!; // Sunrise-Add

    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    private readonly Dictionary<EntityUid, Queue<DoAfterId>> _activeStripDoAfters = new(); // Sunrise-Add

    // Sunrise added start - limit simultaneous stripping by available hands
    private void LimitSimultaneousStripDoAfters(Entity<HandsComponent?> user, DoAfterArgs doAfterArgs)
    {
        var userId = user.Owner;

        if (!TryComp<HandsComponent>(userId, out var handsComp))
            return;

        var handsHolding = 0;
        foreach (var hand in handsComp.Hands.Keys)
        {
            if (_handsSystem.GetHeldItem(user, hand) != null)
                handsHolding++;
        }

        var isPulling = _pullingSystem.IsPulling(userId);
        var freeHands = handsComp.Count - handsHolding;

        if (freeHands == 0 && isPulling)
            freeHands = 0;

        freeHands = Math.Max(0, freeHands);

        if (freeHands == 0)
        {
            if (doAfterArgs.Event is not StrippableDoAfterEvent strippableEvent || !strippableEvent.InsertOrRemove)
            {
                _popupSystem.PopupCursor(Loc.GetString("No hands available!"));
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
    // Sunrise added end

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StrippableComponent, GetVerbsEvent<Verb>>(AddStripVerb);
        SubscribeLocalEvent<StrippableComponent, GetVerbsEvent<ExamineVerb>>(AddStripExamineVerb);

        // BUI
        SubscribeLocalEvent<StrippableComponent, StrippingSlotButtonPressed>(OnStripButtonPressed);

        // DoAfters
        SubscribeLocalEvent<HandsComponent, DoAfterAttemptEvent<StrippableDoAfterEvent>>(OnStrippableDoAfterRunning);
        SubscribeLocalEvent<HandsComponent, StrippableDoAfterEvent>(OnStrippableDoAfterFinished);

        SubscribeLocalEvent<StrippingComponent, CanDropTargetEvent>(OnCanDropOn);
        SubscribeLocalEvent<StrippableComponent, CanDropDraggedEvent>(OnCanDrop);
        SubscribeLocalEvent<StrippableComponent, DragDropDraggedEvent>(OnDragDrop);
        SubscribeLocalEvent<StrippableComponent, ActivateInWorldEvent>(OnActivateInWorld);

        // Sunrise added start - make stripping faster when target is cuffed
        SubscribeLocalEvent<StrippableComponent, BeforeGettingStrippedEvent>(OnBeforeGettingStripped);
        // Sunrise added end
    }

    private void AddStripVerb(EntityUid uid, StrippableComponent component, GetVerbsEvent<Verb> args)
    {
        if (args.Hands == null || !args.CanAccess || !args.CanInteract || args.Target == args.User)
            return;

        Verb verb = new()
        {
            Text = Loc.GetString("strip-verb-get-data-text"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
            Act = () => TryOpenStrippingUi(args.User, (uid, component), true),
        };

        args.Verbs.Add(verb);
    }

    private void AddStripExamineVerb(EntityUid uid, StrippableComponent component, GetVerbsEvent<ExamineVerb> args)
    {
        if (args.Hands == null || !args.CanAccess || !args.CanInteract || args.Target == args.User)
            return;

        ExamineVerb verb = new()
        {
            Text = Loc.GetString("strip-verb-get-data-text"),
            Icon = new SpriteSpecifier.Texture(new("/Textures/Interface/VerbIcons/outfit.svg.192dpi.png")),
            Act = () => TryOpenStrippingUi(args.User, (uid, component), true),
            Category = VerbCategory.Examine,
        };

        args.Verbs.Add(verb);
    }

    private void OnStripButtonPressed(Entity<StrippableComponent> strippable, ref StrippingSlotButtonPressed args)
    {
        if (args.Actor is not { Valid: true } user ||
            !TryComp<HandsComponent>(user, out var userHands))
            return;

        if (args.IsHand)
        {
            StripHand((user, userHands), (strippable.Owner, null), args.Slot, strippable);
            return;
        }

        if (!TryComp<InventoryComponent>(strippable, out var inventory))
            return;

        var hasEnt = _inventorySystem.TryGetSlotEntity(strippable, args.Slot, out var held, inventory);

        if (_handsSystem.GetActiveItem((user, userHands)) is { } activeItem && !hasEnt)
            StartStripInsertInventory((user, userHands), strippable.Owner, activeItem, args.Slot);
        else if (hasEnt)
            StartStripRemoveInventory(user, strippable.Owner, held!.Value, args.Slot);
    }

    private void StripHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        string handId,
        StrippableComponent? targetStrippable)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp) ||
            !Resolve(target, ref targetStrippable))
            return;

        if (!target.Comp.CanBeStripped)
            return;

        var heldEntity = _handsSystem.GetHeldItem(target.Owner, handId);

        // Is the target a handcuff?
        if (TryComp<VirtualItemComponent>(heldEntity, out var virtualItem) &&
            _cuffableSystem.TryGetAllCuffs(target.Owner, out var cuffs) &&
            cuffs.Contains(virtualItem.BlockingEntity))
        {
            _cuffableSystem.TryUncuff(target.Owner, user, virtualItem.BlockingEntity);
            return;
        }

        if (_handsSystem.GetActiveItem(user.AsNullable()) is { } activeItem && heldEntity == null)
            StartStripInsertHand(user, target, activeItem, handId, targetStrippable);
        else if (heldEntity != null)
            StartStripRemoveHand(user, target, heldEntity.Value, handId, targetStrippable);
    }

    /// <summary>
    ///     Checks whether the item is in a user's active hand and whether it can be inserted into the inventory slot.
    /// </summary>
    private bool CanStripInsertInventory(
        Entity<HandsComponent?> user,
        EntityUid target,
        EntityUid held,
        string slot)
    {
        if (!Resolve(user, ref user.Comp))
            return false;

        if (!_handsSystem.TryGetActiveItem(user, out var activeItem) || activeItem != held)
            return false;

        if (!_handsSystem.CanDropHeld(user, user.Comp.ActiveHandId!))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-cannot-drop"));
            return false;
        }

        var targetIdentity = Identity.Entity(target, EntityManager);

        if (_inventorySystem.TryGetSlotEntity(target, slot, out _))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-item-slot-occupied", ("owner", targetIdentity)));
            return false;
        }

        if (!_inventorySystem.CanEquip(user, target, held, slot, out _))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-cannot-equip-message", ("owner", targetIdentity)));
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Begins a DoAfter to insert the item in the user's active hand into the inventory slot.
    /// </summary>
    private void StartStripInsertInventory(
        Entity<HandsComponent?> user,
        EntityUid target,
        EntityUid held,
        string slot)
    {
        if (!Resolve(user, ref user.Comp))
            return;

        if (!CanStripInsertInventory(user, target, held, slot))
            return;

        if (!_inventorySystem.TryGetSlot(target, slot, out var slotDef))
        {
            Log.Error($"{ToPrettyString(user)} attempted to place an item in a non-existent inventory slot ({slot}) on {ToPrettyString(target)}");
            return;
        }

        var (time, stealth) = GetStripTimeModifiers(user, target, held, slotDef.StripTime);

        if (!stealth)
        {
            _popupSystem.PopupEntity(Loc.GetString("strippable-component-alert-owner-insert",
                                                        ("user", Identity.Entity(user, EntityManager)),
                                                        ("item", _handsSystem.GetActiveItem((user, user.Comp))!.Value)),
                                                        target,
                                                        target,
                                                        PopupType.Large);
        }

        var prefix = stealth ? "stealthily " : "";
        _adminLogger.Add(LogType.Stripping, LogImpact.Low, $"{ToPrettyString(user):actor} is trying to {prefix}place the item {ToPrettyString(held):item} in {ToPrettyString(target):target}'s {slot} slot");

        var doAfterArgs = new DoAfterArgs(EntityManager, user, time, new StrippableDoAfterEvent(true, true, slot), user, target, held)
        {
            Hidden = stealth,
            AttemptFrequency = AttemptFrequency.EveryTick,
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTool
        };

        LimitSimultaneousStripDoAfters(user, doAfterArgs);
    }

    /// <summary>
    ///     Inserts the item in the user's active hand into the inventory slot.
    /// </summary>
    private void StripInsertInventory(
        Entity<HandsComponent?> user,
        EntityUid target,
        EntityUid held,
        string slot)
    {
        if (!Resolve(user, ref user.Comp))
            return;

        if (!CanStripInsertInventory(user, target, held, slot))
            return;

        if (!_handsSystem.TryDrop(user))
            return;

        _inventorySystem.TryEquip(user, target, held, slot, triggerHandContact: true);
        _adminLogger.Add(LogType.Stripping, LogImpact.Medium, $"{ToPrettyString(user):actor} has placed the item {ToPrettyString(held):item} in {ToPrettyString(target):target}'s {slot} slot");
    }

    /// <summary>
    ///     Checks whether the item can be removed from the target's inventory.
    /// </summary>
    private bool CanStripRemoveInventory(
        EntityUid user,
        EntityUid target,
        EntityUid item,
        string slot)
    {
        if (!_inventorySystem.TryGetSlotEntity(target, slot, out var slotItem))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-item-slot-free-message", ("owner", Identity.Entity(target, EntityManager))));
            return false;
        }

        if (slotItem != item)
            return false;

        if (!_inventorySystem.CanUnequip(user, target, slot, out var reason))
        {
            _popupSystem.PopupCursor(Loc.GetString(reason));
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Begins a DoAfter to remove the item from the target's inventory and insert it in the user's active hand.
    /// </summary>
    private void StartStripRemoveInventory(
        EntityUid user,
        EntityUid target,
        EntityUid item,
        string slot)
    {
        if (!CanStripRemoveInventory(user, target, item, slot))
            return;

        if (!_inventorySystem.TryGetSlot(target, slot, out var slotDef))
        {
            Log.Error($"{ToPrettyString(user)} attempted to take an item from a non-existent inventory slot ({slot}) on {ToPrettyString(target)}");
            return;
        }

        var (time, stealth) = GetStripTimeModifiers(user, target, item, slotDef.StripTime);

        if (!stealth)
        {
            if (IsStripHidden(slotDef, user))
                _popupSystem.PopupEntity(Loc.GetString("strippable-component-alert-owner-hidden", ("slot", slot)), target, target, PopupType.Large);
            else
            {
                _popupSystem.PopupEntity(Loc.GetString("strippable-component-alert-owner",
                                                            ("user", Identity.Entity(user, EntityManager)),
                                                            ("item", item)),
                                                            target,
                                                            target,
                                                            PopupType.Large);

            }
        }

        var prefix = stealth ? "stealthily " : "";
        _adminLogger.Add(LogType.Stripping, LogImpact.Low, $"{ToPrettyString(user):actor} is trying to {prefix}strip the item {ToPrettyString(item):item} from {ToPrettyString(target):target}'s {slot} slot");

        _interactionSystem.DoContactInteraction(user, item);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, time, new StrippableDoAfterEvent(false, true, slot), user, target, item)
        {
            Hidden = stealth,
            AttemptFrequency = AttemptFrequency.EveryTick,
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            BreakOnHandChange = false, // Allow simultaneously removing multiple items.
            DuplicateCondition = DuplicateConditions.SameTool
        };

        LimitSimultaneousStripDoAfters((user, null), doAfterArgs);
    }

    /// <summary>
    ///     Removes the item from the target's inventory and inserts it in the user's active hand.
    /// </summary>
    private void StripRemoveInventory(
        EntityUid user,
        EntityUid target,
        EntityUid item,
        string slot,
        bool stealth)
    {
        if (!CanStripRemoveInventory(user, target, item, slot))
            return;

        if (!_inventorySystem.TryUnequip(user, target, slot, triggerHandContact: true))
            return;

        RaiseLocalEvent(item, new DroppedEvent(user), true); // Gas tank internals etc.

        _handsSystem.PickupOrDrop(user, item, animateUser: stealth, animate: !stealth);
        _adminLogger.Add(LogType.Stripping, LogImpact.High, $"{ToPrettyString(user):actor} has stripped the item {ToPrettyString(item):item} from {ToPrettyString(target):target}'s {slot} slot");
    }

    /// <summary>
    ///     Checks whether the item in the user's active hand can be inserted into one of the target's hands.
    /// </summary>
    private bool CanStripInsertHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        EntityUid held,
        string handName)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp))
            return false;

        if (!target.Comp.CanBeStripped)
            return false;

        if (!_handsSystem.TryGetActiveItem(user, out var activeItem) || activeItem != held)
            return false;

        if (!_handsSystem.CanDropHeld(user, user.Comp.ActiveHandId!))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-cannot-drop"));
            return false;
        }

        if (!_handsSystem.CanPickupToHand(target, activeItem.Value, handName, checkActionBlocker: false, handsComp: target.Comp))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-cannot-put-message", ("owner", Identity.Entity(target, EntityManager))));
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Begins a DoAfter to insert the item in the user's active hand into one of the target's hands.
    /// </summary>
    private void StartStripInsertHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        EntityUid held,
        string handName,
        StrippableComponent? targetStrippable = null)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp) ||
            !Resolve(target, ref targetStrippable))
            return;

        if (!CanStripInsertHand(user, target, held, handName))
            return;

        var (time, stealth) = GetStripTimeModifiers(user, target, null, targetStrippable.HandStripDelay);

        if (!stealth)
        {
            _popupSystem.PopupEntity(Loc.GetString("strippable-component-alert-owner-insert-hand",
                                                        ("user", Identity.Entity(user, EntityManager)),
                                                        ("item", _handsSystem.GetActiveItem(user)!.Value)),
                                                        target,
                                                        target,
                                                        PopupType.Large);

        }

        var prefix = stealth ? "stealthily " : "";
        _adminLogger.Add(LogType.Stripping, LogImpact.Low, $"{ToPrettyString(user):actor} is trying to {prefix}place the item {ToPrettyString(held):item} in {ToPrettyString(target):target}'s hands");

        var doAfterArgs = new DoAfterArgs(EntityManager, user, time, new StrippableDoAfterEvent(true, false, handName), user, target, held)
        {
            Hidden = stealth,
            AttemptFrequency = AttemptFrequency.EveryTick,
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            DuplicateCondition = DuplicateConditions.SameTool
        };

        LimitSimultaneousStripDoAfters(user, doAfterArgs);
    }

    /// <summary>
    ///     Places the item in the user's active hand into one of the target's hands.
    /// </summary>
    private void StripInsertHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        EntityUid held,
        string handName,
        bool stealth)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp))
            return;

        if (!CanStripInsertHand(user, target, held, handName))
            return;

        _handsSystem.TryDrop(user, checkActionBlocker: false);
        _handsSystem.TryPickup(target, held, handName, checkActionBlocker: false, animateUser: stealth, animate: !stealth, handsComp: target.Comp);
        _adminLogger.Add(LogType.Stripping, LogImpact.Medium, $"{ToPrettyString(user):actor} has placed the item {ToPrettyString(held):item} in {ToPrettyString(target):target}'s hands");

        // Hand update will trigger strippable update.
    }

    /// <summary>
    ///     Checks whether the item is in the target's hand and whether it can be dropped.
    /// </summary>
    private bool CanStripRemoveHand(
        EntityUid user,
        Entity<HandsComponent?> target,
        EntityUid item,
        string handName)
    {
        if (!Resolve(target, ref target.Comp))
            return false;

        if (!target.Comp.CanBeStripped)
            return false;

        if (!_handsSystem.TryGetHand(target, handName, out _))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-item-slot-free-message", ("owner", Identity.Entity(target, EntityManager))));
            return false;
        }

        if (!_handsSystem.TryGetHeldItem(target, handName, out var heldEntity))
            return false;

        if (HasComp<VirtualItemComponent>(heldEntity))
            return false;

        if (heldEntity != item)
            return false;

        if (!_handsSystem.CanDropHeld(target, handName, false))
        {
            _popupSystem.PopupCursor(Loc.GetString("strippable-component-cannot-drop-message", ("owner", Identity.Entity(target, EntityManager))));
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Begins a DoAfter to remove the item from the target's hand and insert it in the user's active hand.
    /// </summary>
    private void StartStripRemoveHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        EntityUid item,
        string handName,
        StrippableComponent? targetStrippable = null)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp) ||
            !Resolve(target, ref targetStrippable))
            return;

        if (!CanStripRemoveHand(user, target, item, handName))
            return;

        var (time, stealth) = GetStripTimeModifiers(user, target, null, targetStrippable.HandStripDelay);

        if (!stealth)
        {
            _popupSystem.PopupEntity(Loc.GetString("strippable-component-alert-owner",
                                                        ("user", Identity.Entity(user, EntityManager)),
                                                        ("item", item)),
                                                        target,
                                                        target);
        }

        var prefix = stealth ? "stealthily " : "";
        _adminLogger.Add(LogType.Stripping, LogImpact.Low, $"{ToPrettyString(user):actor} is trying to {prefix}strip the item {ToPrettyString(item):item} from {ToPrettyString(target):target}'s hands");

        _interactionSystem.DoContactInteraction(user, item);

        var doAfterArgs = new DoAfterArgs(EntityManager, user, time, new StrippableDoAfterEvent(false, false, handName), user, target, item)
        {
            Hidden = stealth,
            AttemptFrequency = AttemptFrequency.EveryTick,
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
            BreakOnHandChange = false, // Allow simultaneously removing multiple items.
            DuplicateCondition = DuplicateConditions.SameTool
        };

        LimitSimultaneousStripDoAfters(user, doAfterArgs);
    }

    /// <summary>
    ///     Takes the item from the target's hand and inserts it in the user's active hand.
    /// </summary>
    private void StripRemoveHand(
        Entity<HandsComponent?> user,
        Entity<HandsComponent?> target,
        EntityUid item,
        string handName,
        bool stealth)
    {
        if (!Resolve(user, ref user.Comp) ||
            !Resolve(target, ref target.Comp))
            return;

        if (!CanStripRemoveHand(user, target, item, handName))
            return;

        _handsSystem.TryDrop(target, item, checkActionBlocker: false);
        _handsSystem.PickupOrDrop(user, item, animateUser: stealth, animate: !stealth, handsComp: user.Comp);
        _adminLogger.Add(LogType.Stripping, LogImpact.High, $"{ToPrettyString(user):actor} has stripped the item {ToPrettyString(item):item} from {ToPrettyString(target):target}'s hands");

        // Hand update will trigger strippable update.
    }

    private void OnStrippableDoAfterRunning(Entity<HandsComponent> entity, ref DoAfterAttemptEvent<StrippableDoAfterEvent> ev)
    {
        var args = ev.DoAfter.Args;

        DebugTools.Assert(entity.Owner == args.User);
        DebugTools.Assert(args.Target != null);
        DebugTools.Assert(args.Used != null);
        DebugTools.Assert(ev.Event.SlotOrHandName != null);

        // Sunrise added start - revalidate hand capacity while doafter is running
        if (TryComp<HandsComponent>(entity.Owner, out var handsComp))
        {
            var handsHolding = 0;
            foreach (var hand in handsComp.Hands.Keys)
            {
                if (_handsSystem.GetHeldItem((entity.Owner, entity.Comp), hand) != null)
                    handsHolding++;
            }

            var freeHands = Math.Max(0, handsComp.Count - handsHolding);
            if (freeHands == 0)
            {
                if (!ev.Event.InsertOrRemove)
                    ev.Cancel();
            }
            else if (_activeStripDoAfters.TryGetValue(entity.Owner, out var queue))
            {
                var excess = queue.Count - freeHands;
                if (excess > 0)
                {
                    var queueList = queue.ToList();
                    var newestExcess = queueList.GetRange(queueList.Count - excess, excess);
                    if (newestExcess.Contains(ev.DoAfter.Id))
                        ev.Cancel();
                }
            }
        }
        // Sunrise added end

        if (ev.Event.InventoryOrHand)
        {
            if ( ev.Event.InsertOrRemove && !CanStripInsertInventory((entity.Owner, entity.Comp), args.Target.Value, args.Used.Value, ev.Event.SlotOrHandName) ||
                !ev.Event.InsertOrRemove && !CanStripRemoveInventory(entity.Owner, args.Target.Value, args.Used.Value, ev.Event.SlotOrHandName))
            {
                ev.Cancel();
            }
        }
        else
        {
            if ( ev.Event.InsertOrRemove && !CanStripInsertHand((entity.Owner, entity.Comp), args.Target.Value, args.Used.Value, ev.Event.SlotOrHandName) ||
                !ev.Event.InsertOrRemove && !CanStripRemoveHand(entity.Owner, args.Target.Value, args.Used.Value, ev.Event.SlotOrHandName))
            {
                ev.Cancel();
            }
        }

        // Sunrise added start - cleanup tracking when doafter is cancelled
        if (ev.Cancelled && _activeStripDoAfters.TryGetValue(entity.Owner, out var cleanupQueue))
        {
            var toRemove = ev.DoAfter.Id;
            var newQueue = new Queue<DoAfterId>(cleanupQueue.Count);
            while (cleanupQueue.Count > 0)
            {
                var id = cleanupQueue.Dequeue();
                if (id != toRemove)
                    newQueue.Enqueue(id);
            }

            _activeStripDoAfters[entity.Owner] = newQueue;
        }
        // Sunrise added end
    }

    private void OnStrippableDoAfterFinished(Entity<HandsComponent> entity, ref StrippableDoAfterEvent ev)
    {
        if (ev.Cancelled)
            return;

        DebugTools.Assert(entity.Owner == ev.User);
        DebugTools.Assert(ev.Target != null);
        DebugTools.Assert(ev.Used != null);
        DebugTools.Assert(ev.SlotOrHandName != null);

        // Sunrise added start - cleanup tracking when doafter finishes
        if (_activeStripDoAfters.TryGetValue(entity.Owner, out var queue))
        {
            var toRemove = ev.DoAfter.Id;
            var newQueue = new Queue<DoAfterId>(queue.Count);
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (id != toRemove)
                    newQueue.Enqueue(id);
            }

            _activeStripDoAfters[entity.Owner] = newQueue;
        }
        // Sunrise added end

        if (ev.InventoryOrHand)
        {
            if (ev.InsertOrRemove)
                StripInsertInventory((entity.Owner, entity.Comp), ev.Target.Value, ev.Used.Value, ev.SlotOrHandName);
            else
                StripRemoveInventory(entity.Owner, ev.Target.Value, ev.Used.Value, ev.SlotOrHandName, ev.Args.Hidden);
        }
        else
        {
            if (ev.InsertOrRemove)
                StripInsertHand((entity.Owner, entity.Comp), ev.Target.Value, ev.Used.Value, ev.SlotOrHandName, ev.Args.Hidden);
            else
                StripRemoveHand((entity.Owner, entity.Comp), ev.Target.Value, ev.Used.Value, ev.SlotOrHandName, ev.Args.Hidden);
        }
    }

    private void OnActivateInWorld(EntityUid uid, StrippableComponent component, ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex || args.Target == args.User)
            return;

        if (TryOpenStrippingUi(args.User, (uid, component)))
            args.Handled = true;
    }

    // Sunrise added start - make stripping faster when target is cuffed
    private void OnBeforeGettingStripped(EntityUid uid, StrippableComponent component, ref BeforeGettingStrippedEvent ev)
    {
        if (!TryComp<CuffableComponent>(uid, out var cuffable))
            return;

        var entity = new Entity<CuffableComponent>(uid, cuffable);
        if (_cuffableSystem.IsCuffed(entity))
            ev.Multiplier *= 0.5f;
    }
    // Sunrise added end

    /// <summary>
    /// Modify the strip time via events. Raised directed at the item being stripped, the player stripping someone and the player being stripped.
    /// </summary>
    public (TimeSpan Time, bool Stealth) GetStripTimeModifiers(EntityUid user, EntityUid targetPlayer, EntityUid? targetItem, TimeSpan initialTime)
    {
        var itemEv = new BeforeItemStrippedEvent(initialTime, false);
        if (targetItem != null)
            RaiseLocalEvent(targetItem.Value, ref itemEv);
        var userEv = new BeforeStripEvent(itemEv.Time, itemEv.Stealth);
        RaiseLocalEvent(user, ref userEv);
        var targetEv = new BeforeGettingStrippedEvent(userEv.Time, userEv.Stealth);
        RaiseLocalEvent(targetPlayer, ref targetEv);
        return (targetEv.Time, targetEv.Stealth);
    }

    private void OnDragDrop(EntityUid uid, StrippableComponent component, ref DragDropDraggedEvent args)
    {
        // If the user drags a strippable thing onto themselves.
        if (args.Handled || args.Target != args.User)
            return;

        // Sunrise-Start
        if (!TryComp<StrippingComponent>(args.Target, out var strippingComp) || !strippingComp.UseDragDrop)
        {
            return;
        }
        // Sunrise-End

        if (TryOpenStrippingUi(args.User, (uid, component)))
            args.Handled = true;
    }

    public bool TryOpenStrippingUi(EntityUid user, Entity<StrippableComponent> target, bool openInCombat = false)
    {
        if (!openInCombat && TryComp<CombatModeComponent>(user, out var mode) && mode.IsInCombatMode)
            return false;

        if (!HasComp<StrippingComponent>(user))
            return false;

        _ui.OpenUi(target.Owner, StrippingUiKey.Key, user);
        return true;
    }

    private void OnCanDropOn(EntityUid uid, StrippingComponent component, ref CanDropTargetEvent args)
    {
        var val = uid == args.User &&
                  HasComp<StrippableComponent>(args.Dragged) &&
                  HasComp<HandsComponent>(args.User) &&
                  HasComp<StrippingComponent>(args.User);
        args.Handled |= val;
        args.CanDrop |= val;
    }

    private void OnCanDrop(EntityUid uid, StrippableComponent component, ref CanDropDraggedEvent args)
    {
        args.CanDrop |= args.Target == args.User &&
                        HasComp<StrippingComponent>(args.User) &&
                        HasComp<HandsComponent>(args.User);

        if (args.CanDrop)
            args.Handled = true;
    }

    public bool IsStripHidden(SlotDefinition definition, EntityUid? viewer)
    {
        if (!definition.StripHidden)
            return false;

        if (viewer == null)
            return true;

        return !HasComp<BypassInteractionChecksComponent>(viewer);
    }
}
