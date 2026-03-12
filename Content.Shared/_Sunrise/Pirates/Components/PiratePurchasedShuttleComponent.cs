using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Pirates.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PiratePurchasedShuttleComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Station;

    [DataField, AutoNetworkedField]
    public int PurchasePrice;
}
