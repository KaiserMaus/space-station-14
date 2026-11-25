using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Research.Components;

public sealed partial class TechnologyDatabaseComponent
{
    /// <summary>
    /// Technologies whose recipes were stolen while their research history remains unlocked.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<TechnologyPrototype>> StolenTechnologies = [];

    /// <summary>
    /// Recipes restored by technology disks independently of their source technologies.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<LatheRecipePrototype>> DiskRestoredRecipes = [];

    /// <summary>
    /// Дополнительные дисциплины, выбранные после расширения базы исследований.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<TechDisciplinePrototype>> AdditionalDisciplines = [];

    /// <summary>
    /// Максимальное число дисциплин, доступных после порога блокировки.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ResearchDisciplineLimit = 1;

    /// <summary>
    /// Максимальный уровень целых технологий, которые можно исследовать в этой базе.
    /// Рецепты, импортированные отдельно, этим лимитом не ограничиваются.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int? MaximumTechnologyTier;
}
