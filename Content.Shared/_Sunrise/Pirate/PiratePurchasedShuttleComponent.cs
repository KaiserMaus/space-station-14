using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Pirate;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PiratePurchasedShuttleComponent : Component
{
    /// <summary>
    /// Pirate station that owns this purchased shuttle.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Station;

    /// <summary>
    /// Purchase price used for sell-back refunds.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int PurchasePrice;
}
