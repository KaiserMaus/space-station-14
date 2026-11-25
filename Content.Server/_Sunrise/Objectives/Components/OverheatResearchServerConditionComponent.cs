using Content.Server._Sunrise.Objectives.Systems;

namespace Content.Server._Sunrise.Objectives.Components;

/// <summary>
/// Хранит факт запуска необратимого перегрева полноценного исследовательского сервера.
/// </summary>
[RegisterComponent, Access(typeof(OverheatResearchServerConditionSystem))]
public sealed partial class OverheatResearchServerConditionComponent : Component
{
    [DataField]
    public bool Completed;
}
