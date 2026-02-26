using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.Documents;

[RegisterComponent]
public sealed partial class XmlPaperContentComponent : Component
{
    [DataField("path", required: true)]
    public ResPath Path = default!;

    [DataField("header")]
    public SpriteSpecifier? Header;
}
