namespace Content.Server._Sunrise.Magic.InfiniteGuns;

[RegisterComponent]
public sealed partial class PendingEnchantedGunThrowComponent : Component
{
    /// <summary>
    /// Стрелок, из рук которого нужно выбросить оружие.
    /// </summary>
    public EntityUid User;
}
