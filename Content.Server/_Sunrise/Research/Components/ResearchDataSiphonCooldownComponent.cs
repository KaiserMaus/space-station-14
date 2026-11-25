using Content.Server._Sunrise.Research.Systems;

namespace Content.Server._Sunrise.Research.Components;

/// <summary>
/// Не позволяет повторно активировать обезвреженный ретранслятор до окончания перезарядки.
/// </summary>
[RegisterComponent, Access(typeof(ResearchDataSiphonSystem))]
public sealed partial class ResearchDataSiphonCooldownComponent : Component
{
    /// <summary>
    /// Игровое время окончания перезарядки.
    /// </summary>
    public TimeSpan EndsAt;
}
