using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Weapons.Ranged;

/// <summary>
/// Обозначает оружие, у которого затвор управляет доступом к коробу с лентой.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BeltFeedSystem))]
public sealed partial class BeltFeedComponent : Component
{
    /// <summary>
    /// Требуется ли передёрнуть затвор перед следующей стрельбой.
    /// </summary>
    [AutoNetworkedField]
    public bool RequiresCycle;
}
