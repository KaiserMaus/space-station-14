using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Magic.InfiniteGuns;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class EnchantedGunComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public EntProtoId OrdinaryPrototype;
}
