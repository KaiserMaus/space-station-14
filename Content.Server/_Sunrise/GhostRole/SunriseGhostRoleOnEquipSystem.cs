using Content.Server.Ghost.Roles.Components;
using Content.Server.Ghost;
using Content.Shared._Sunrise.GhostRole.Components;
using Content.Shared.Clothing;
using Content.Shared.Ghost;
using Content.Shared.Inventory.Events;
using Content.Shared.Mind.Components;

namespace Content.Server._Sunrise.GhostRole;

public sealed class SunriseGhostRoleOnEquipSystem : EntitySystem
{
    [Dependency] private readonly GhostSystem _ghost = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SunriseGhostRoleOnEquipComponent, ClothingGotEquippedEvent>(OnClothingGotEquipped);
        SubscribeLocalEvent<SunriseGhostRoleOnEquipComponent, ClothingGotUnequippedEvent>(OnClothingGotUnequipped);
        SubscribeLocalEvent<DidUnequipEvent>(OnDidUnequip);
    }

    private void OnClothingGotEquipped(Entity<SunriseGhostRoleOnEquipComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (HasComp<GhostComponent>(args.Wearer))
            return;

        if (TryComp<MindContainerComponent>(args.Wearer, out var mindContainer) && mindContainer.HasMind)
            return;

        var ghostRole = EnsureComp<GhostRoleComponent>(args.Wearer);
        EnsureComp<GhostTakeoverAvailableComponent>(args.Wearer);

        ghostRole.RoleName = ent.Comp.RoleName;
        ghostRole.RoleDescription = ent.Comp.RoleDescription;
        ghostRole.RoleRules = ent.Comp.RoleRules;
        ghostRole.MindRoles = ent.Comp.MindRoles;
    }

    private void OnClothingGotUnequipped(Entity<SunriseGhostRoleOnEquipComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        CleanupGhostRole(args.Wearer);
    }

    private void OnDidUnequip(DidUnequipEvent args)
    {
        if (!HasComp<SunriseGhostRoleOnEquipComponent>(args.Equipment))
            return;

        CleanupGhostRole(args.Equipee);
    }

    private void CleanupGhostRole(EntityUid wearer)
    {
        if (!HasComp<GhostRoleComponent>(wearer) && !HasComp<GhostTakeoverAvailableComponent>(wearer))
            return;

        RemComp<GhostTakeoverAvailableComponent>(wearer);

        if (TryComp<MindContainerComponent>(wearer, out var mindContainer) && mindContainer.HasMind)
            _ghost.OnGhostAttempt(mindContainer.Mind!.Value, false, true, true);

        RemComp<GhostRoleComponent>(wearer);
    }
}
