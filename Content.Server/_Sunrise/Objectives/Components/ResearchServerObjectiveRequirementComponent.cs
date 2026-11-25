using Content.Server._Sunrise.Objectives.Systems;

namespace Content.Server._Sunrise.Objectives.Components;

/// <summary>
/// Требует наличия подходящего исследовательского сервера на гриде станции.
/// </summary>
[RegisterComponent, Access(typeof(ResearchServerObjectiveRequirementSystem))]
public sealed partial class ResearchServerObjectiveRequirementComponent : Component
{
    /// <summary>
    /// Учитывать ли исследовательские базы с ограничением максимального уровня технологий.
    /// </summary>
    [DataField]
    public bool AllowLimitedServers;
}
