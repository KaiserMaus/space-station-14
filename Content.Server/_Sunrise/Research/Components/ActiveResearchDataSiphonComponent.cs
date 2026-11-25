using Content.Server._Sunrise.Research.Systems;

namespace Content.Server._Sunrise.Research.Components;

/// <summary>
/// Состояние работающего ретранслятора; время учитывается только при наличии питания.
/// </summary>
[RegisterComponent, Access(typeof(ResearchDataSiphonSystem))]
public sealed partial class ActiveResearchDataSiphonComponent : Component
{
    /// <summary>
    /// Суммарное время работы при наличии питания.
    /// </summary>
    public TimeSpan PoweredElapsed;

    /// <summary>
    /// Момент следующей кражи по шкале <see cref="PoweredElapsed"/>.
    /// </summary>
    [DataField]
    public TimeSpan NextTheftAt = TimeSpan.FromSeconds(30);
}
