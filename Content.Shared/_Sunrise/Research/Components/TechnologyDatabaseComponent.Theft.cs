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
}
