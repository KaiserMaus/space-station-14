using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Pirates.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PirateBaseComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? AssociatedRule;
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PirateStationComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity? AssociatedRule;

    [DataField, AutoNetworkedField]
    public EntityUid? CurrentShuttle;

    [DataField, AutoNetworkedField]
    public int CurrentShuttleValue;
}
