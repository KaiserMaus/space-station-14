using Content.Shared.Weapons.Ranged.Components;

namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    /// <summary>
    /// Полностью очищает встроенный поставщик патронов, включая сетевой список сущностей боеприпасов.
    /// </summary>
    public void EmptyBallisticAmmo(Entity<BallisticAmmoProviderComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        while (entity.Comp.Container.ContainedEntities.Count > 0)
            Del(entity.Comp.Container.ContainedEntities[^1]);

        entity.Comp.Entities.Clear();
        entity.Comp.UnspawnedCount = 0;
        UpdateBallisticAppearance((entity.Owner, entity.Comp));
        UpdateAmmoCount(entity.Owner);
        Dirty(entity);
    }

    /// <summary>
    /// Управляет сбросом задержки выстрела при выборе оружия в руке.
    /// </summary>
    public void SetResetOnHandSelected(Entity<GunComponent?> gun, bool value)
    {
        if (!Resolve(gun, ref gun.Comp, false) || gun.Comp.ResetOnHandSelected == value)
            return;

        gun.Comp.ResetOnHandSelected = value;
        Dirty(gun);
    }
}
