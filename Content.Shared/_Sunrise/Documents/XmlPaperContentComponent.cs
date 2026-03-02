using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Documents;

[RegisterComponent, ComponentProtoName("XmlPaperContent")]
public sealed partial class XmlPaperContentComponent : Component
{
    [DataField("path", required: true)]
    public ResPath Path = default!;

    [DataField("header")]
    public SpriteSpecifier? Header;
}
