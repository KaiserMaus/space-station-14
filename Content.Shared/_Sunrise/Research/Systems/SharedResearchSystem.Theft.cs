using System.Collections.Generic;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

// ReSharper disable once CheckNamespace
namespace Content.Shared.Research.Systems;

public abstract partial class SharedResearchSystem
{
    /// <summary>
    /// Returns researched technologies that may currently be stolen from the database.
    /// </summary>
    public List<TechnologyPrototype> GetStealableTechnologies(
        TechnologyDatabaseComponent database,
        IReadOnlySet<ProtoId<TechnologyPrototype>>? excluded = null)
    {
        var technologies = new List<TechnologyPrototype>();
        foreach (var technologyId in database.UnlockedTechnologies)
        {
            if (database.StolenTechnologies.Contains(technologyId) ||
                excluded?.Contains(technologyId) == true ||
                !ProtoMan.TryIndex(technologyId, out var technology) ||
                technology.RequiresResearchCompletion)
            {
                continue;
            }

            technologies.Add(technology);
        }

        return technologies;
    }

    /// <summary>
    /// Marks a technology as stolen and removes recipes that have no remaining source.
    /// Research history and prerequisite progress are preserved.
    /// </summary>
    public bool TryStealTechnology(
        Entity<TechnologyDatabaseComponent?> database,
        ProtoId<TechnologyPrototype> technologyId)
    {
        if (!Resolve(database, ref database.Comp) ||
            !database.Comp.UnlockedTechnologies.Contains(technologyId) ||
            database.Comp.StolenTechnologies.Contains(technologyId) ||
            !ProtoMan.TryIndex(technologyId, out var technology) ||
            technology.RequiresResearchCompletion)
        {
            return false;
        }

        database.Comp.StolenTechnologies.Add(technologyId);
        foreach (var recipe in technology.RecipeUnlocks)
        {
            if (database.Comp.DiskRestoredRecipes.Contains(recipe) || HasActiveTechnologySource(database.Comp, recipe))
                continue;

            database.Comp.UnlockedRecipes.Remove(recipe);
        }

        Dirty(database);
        UpdateTechnologyCards(database, database.Comp);

        var ev = new TechnologyDatabaseModifiedEvent(null);
        RaiseLocalEvent(database, ref ev);
        return true;
    }

    /// <summary>
    /// Restores a recipe from a technology disk and records its independent source.
    /// </summary>
    public void AddTechnologyDiskRecipe(
        Entity<TechnologyDatabaseComponent?> database,
        ProtoId<LatheRecipePrototype> recipe)
    {
        if (!Resolve(database, ref database.Comp))
            return;

        var provenanceAdded = false;
        if (!database.Comp.DiskRestoredRecipes.Contains(recipe))
        {
            database.Comp.DiskRestoredRecipes.Add(recipe);
            provenanceAdded = true;
        }

        var recipeAdded = false;
        if (!database.Comp.UnlockedRecipes.Contains(recipe))
        {
            database.Comp.UnlockedRecipes.Add(recipe);
            recipeAdded = true;
        }

        if (!provenanceAdded && !recipeAdded)
            return;

        Dirty(database);
        if (!recipeAdded)
            return;

        var ev = new TechnologyDatabaseModifiedEvent([recipe.Id]);
        RaiseLocalEvent(database, ref ev);
    }

    private bool HasActiveTechnologySource(
        TechnologyDatabaseComponent database,
        ProtoId<LatheRecipePrototype> recipe)
    {
        foreach (var technologyId in database.UnlockedTechnologies)
        {
            if (database.StolenTechnologies.Contains(technologyId) ||
                !ProtoMan.TryIndex(technologyId, out var technology))
            {
                continue;
            }

            if (technology.RecipeUnlocks.Contains(recipe))
                return true;
        }

        return false;
    }
}
