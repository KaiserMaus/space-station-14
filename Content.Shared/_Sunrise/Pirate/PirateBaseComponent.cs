using JetBrains.Annotations;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Pirate;

/// <summary>
/// Tags a grid as the pirate base, making it station-like for pirate trade.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PirateBaseComponent : Component
{
    /// <summary>
    /// Game rule that loaded this base.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid AssociatedRule;
}

/// <summary>
/// Tags an entity as the pirate station created from a pirate base grid.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, UsedImplicitly]
public sealed partial class PirateStationComponent : Component
{
    /// <summary>
    /// Game rule associated with this pirate station.
    /// </summary>
    [AutoNetworkedField]
    public NetEntity? AssociatedRule;

    /// <summary>
    /// Shuttle currently deployed through the pirate shipyard console.
    /// </summary>
    [AutoNetworkedField]
    public EntityUid? CurrentShuttle;

    /// <summary>
    /// Recorded value of the currently deployed shuttle.
    /// </summary>
    [AutoNetworkedField]
    public int CurrentShuttleValue;
}
