using Content.Shared.Hands;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory.VirtualItem;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Systems;

namespace Content.Shared._Sunrise.Magic.InfiniteGuns;

public sealed partial class EnchantedGunRestrictionSystem : EntitySystem
{
    [Dependency] private SharedGunSystem _gun = default!;
    [Dependency] private SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HandsComponent, DropAttemptEvent>(OnDropAttempt);
        SubscribeLocalEvent<HandsComponent, BeforeThrowEvent>(OnBeforeThrow);
    }

    private void OnDropAttempt(Entity<HandsComponent> ent, ref DropAttemptEvent args)
    {
        if (args.Cancelled || !_hands.TryGetActiveItem(ent.AsNullable(), out var held))
            return;

        if (TryResolveEnchantedGun(held.Value, out var gun) && !CanDiscard(gun))
            args.Cancel();
    }

    private void OnBeforeThrow(Entity<HandsComponent> ent, ref BeforeThrowEvent args)
    {
        if (args.Cancelled)
            return;

        if (TryResolveEnchantedGun(args.ItemUid, out var gun) && !CanDiscard(gun))
            args.Cancelled = true;
    }

    public bool CanDiscard(Entity<EnchantedGunComponent> gun)
    {
        return _gun.GetAmmoCount(gun) == 0;
    }

    private bool TryResolveEnchantedGun(EntityUid item, out Entity<EnchantedGunComponent> gun)
    {
        if (TryComp(item, out EnchantedGunComponent? enchanted))
        {
            gun = (item, enchanted);
            return true;
        }

        if (TryComp(item, out VirtualItemComponent? virtualItem) &&
            TryComp(virtualItem.BlockingEntity, out enchanted))
        {
            gun = (virtualItem.BlockingEntity, enchanted);
            return true;
        }

        gun = default;
        return false;
    }
}
