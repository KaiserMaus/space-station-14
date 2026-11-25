using Content.Server._Sunrise.Research.Systems;

namespace Content.Server._Sunrise.Research.Components;

/// <summary>
/// Хранит постоянную привязку ретранслятора к разуму агента и его цели.
/// </summary>
[RegisterComponent, Access(typeof(ResearchDataSiphonSystem))]
public sealed partial class ResearchDataSiphonComponent : Component
{
    /// <summary>
    /// Разум агента, впервые активировавшего ретранслятор.
    /// </summary>
    public EntityUid? BoundMind;

    /// <summary>
    /// Цель кражи технологий, для которой был активирован ретранслятор.
    /// </summary>
    public EntityUid? BoundObjective;

    /// <summary>
    /// Радиус, в котором все пересечённые тайлы должны иметь пол.
    /// </summary>
    [DataField]
    public float PlacementSafetyRadius = 5f;

    /// <summary>
    /// Продолжительность обезвреживания ретранслятора.
    /// </summary>
    [DataField]
    public TimeSpan DeactivationLength = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Время до повторного закрепления после успешного обезвреживания.
    /// </summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(20);
}
