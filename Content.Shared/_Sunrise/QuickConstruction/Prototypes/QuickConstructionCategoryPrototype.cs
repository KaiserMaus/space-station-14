using Content.Shared.Construction.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.QuickConstruction.Prototypes;

[Prototype("quickConstructionCategory")]
public sealed partial class QuickConstructionCategoryPrototype : IPrototype
{
    [ViewVariables]
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public string Name = string.Empty;

    [DataField]
    public SpriteSpecifier? Icon;

    [DataField]
    public List<ProtoId<ConstructionPrototype>> ConstructionEntries = [];

    [DataField]
    public List<ProtoId<QuickConstructionCategoryPrototype>> CategoryEntries = [];
}
