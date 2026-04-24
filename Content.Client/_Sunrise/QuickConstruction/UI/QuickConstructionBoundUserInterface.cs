using System.Linq;
using Content.Client.Construction;
using Content.Client.UserInterface.Controls;
using Content.Shared._Sunrise.QuickConstruction.Components;
using Content.Shared._Sunrise.QuickConstruction.Prototypes;
using Content.Shared.Construction.Prototypes;
using JetBrains.Annotations;
using Robust.Client.Placement;
using Robust.Client.UserInterface;
using Robust.Shared.Collections;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;

namespace Content.Client._Sunrise.QuickConstruction.UI;

[UsedImplicitly]
public sealed class QuickConstructionBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IPlacementManager _placement = default!;

    private SimpleRadialMenu? _menu;

    public QuickConstructionBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<QuickConstructableComponent>(Owner, out var quickConstructable) ||
            !_prototypeManager.TryIndex(quickConstructable.Category, out var rootCategory))
            return;

        var models = ConvertToButtons(
            rootCategory.ConstructionEntries,
            rootCategory.CategoryEntries,
            [quickConstructable.Category]);

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.SetButtons(models);
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> ConvertToButtons(
        List<ProtoId<ConstructionPrototype>> constructionEntries,
        List<ProtoId<QuickConstructionCategoryPrototype>> categoryEntries,
        HashSet<ProtoId<QuickConstructionCategoryPrototype>> categoryStack)
    {
        var constructionSystem = EntMan.System<ConstructionSystem>();

        ValueList<RadialMenuActionOptionBase> constructionButtons = [];
        ValueList<(QuickConstructionCategoryPrototype Prototype, IReadOnlyCollection<RadialMenuOptionBase> Buttons)> categoryButtons = [];

        foreach (var constructionEntry in constructionEntries)
        {
            if (!_prototypeManager.TryIndex(constructionEntry, out var constructionPrototype) ||
                !constructionSystem.TryGetRecipePrototype(constructionEntry, out var recipePrototypeId) ||
                !_prototypeManager.TryIndex(recipePrototypeId, out var recipePrototype))
                continue;

            var topLevelActionOption = new RadialMenuActionOption<ConstructionPrototype>(HandlePlacement, constructionPrototype)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(recipePrototype.ID),
                ToolTip = recipePrototype.Name,
            };

            constructionButtons.Add(topLevelActionOption);
        }

        foreach (var categoryEntry in categoryEntries)
        {
            if (categoryStack.Contains(categoryEntry) ||
                !_prototypeManager.TryIndex(categoryEntry, out var categoryPrototype))
                continue;

            categoryStack.Add(categoryEntry);
            var nestedButtons = ConvertToButtons(
                categoryPrototype.ConstructionEntries,
                categoryPrototype.CategoryEntries,
                categoryStack).ToList();
            categoryStack.Remove(categoryEntry);

            categoryButtons.Add((categoryPrototype, nestedButtons));
        }

        var models = new List<RadialMenuOptionBase>(categoryButtons.Count + constructionButtons.Count);

        foreach (var (categoryPrototype, buttonList) in categoryButtons)
        {
            models.Add(new RadialMenuNestedLayerOption(buttonList)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(categoryPrototype.Icon),
                ToolTip = string.IsNullOrWhiteSpace(categoryPrototype.Name)
                    ? categoryPrototype.ID
                    : Loc.GetString(categoryPrototype.Name),
            });
        }

        models.AddRange(constructionButtons);
        return models;
    }

    private void HandlePlacement(ConstructionPrototype prototype)
    {
        var constructionSystem = EntMan.System<ConstructionSystem>();

        if (prototype.Type == ConstructionType.Item)
        {
            constructionSystem.TryStartItemConstruction(prototype.ID);
            _menu?.Close();
            return;
        }

        _placement.BeginPlacing(new PlacementInformation
            {
                IsTile = false,
                PlacementOption = prototype.PlacementMode,
            },
            new ConstructionPlacementHijack(constructionSystem, prototype));

        _menu?.Close();
    }
}
