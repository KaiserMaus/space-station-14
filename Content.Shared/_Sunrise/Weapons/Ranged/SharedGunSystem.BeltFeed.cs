using Content.Shared._Sunrise.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Components;

// Файл намеренно находится в _Sunrise, но расширяет ванильный partial-класс.
#pragma warning disable IDE0130
namespace Content.Shared.Weapons.Ranged.Systems;

public abstract partial class SharedGunSystem
{
    [Dependency] private BeltFeedSystem _beltFeed = default!;

    private bool TryHandleBeltFeedCock(EntityUid uid, EntityUid user)
    {
        if (!TryComp<BeltFeedComponent>(uid, out var beltFeed))
            return false;

        _beltFeed.TryCycle((uid, beltFeed), user);
        return true;
    }

    /// <summary>
    /// Пытается вручную извлечь патрон из патронника без автоматического досылания следующего.
    /// </summary>
    public bool TryEjectChamberedRound(
        Entity<ChamberMagazineAmmoProviderComponent?> ent,
        EntityUid? user = null)
    {
        if (!Resolve(ent, ref ent.Comp) || !CanEjectChamberedRound(ent))
            return false;

        DoEjectChamberedRound((ent.Owner, ent.Comp), user);
        return true;
    }

    /// <summary>
    /// Проверяет, есть ли в оружии патрон, который можно извлечь вручную.
    /// </summary>
    public bool CanEjectChamberedRound(Entity<ChamberMagazineAmmoProviderComponent?> ent)
    {
        return Resolve(ent, ref ent.Comp) && GetChamberEntity(ent) != null;
    }

    private void DoEjectChamberedRound(
        Entity<ChamberMagazineAmmoProviderComponent> ent,
        EntityUid? user)
    {
        if (!TryTakeChamberEntity(ent, out var chambered))
            return;

        if (_netManager.IsServer)
            EjectCartridge(chambered.Value);
        else
            TransformSystem.DetachEntity(chambered.Value, Transform(chambered.Value));

        UpdateAmmoCount(ent);
        Audio.PlayPredicted(ent.Comp.RackSound, ent, user);
    }
}
