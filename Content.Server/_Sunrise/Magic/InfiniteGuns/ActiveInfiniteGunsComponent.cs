using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Magic.InfiniteGuns;

[RegisterComponent]
public sealed partial class ActiveInfiniteGunsComponent : Component
{
    /// <summary>
    /// Прототип оружия, создаваемого текущим потоком.
    /// </summary>
    public EntProtoId WeaponPrototype;

    /// <summary>
    /// Количество предметов, которые ещё разрешено создать.
    /// </summary>
    public int RemainingCount;

    /// <summary>
    /// Максимальное количество предметов в одной выдаче.
    /// </summary>
    public int BatchSize;

    /// <summary>
    /// Предотвращает повторный запуск выдачи из событий рук во время текущей выдачи.
    /// </summary>
    public bool Spawning;
}
