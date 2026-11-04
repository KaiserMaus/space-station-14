using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Ninja.Components;

/// <summary>
/// Allows a ninja to extract by hacking an escape-pod console.
/// The ability is removed after a successful extraction.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaPodHackerComponent : Component
{
    /// <summary>
    /// Time required to hack the console.
    /// </summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(20);

    /// <summary>
    /// Delay between a successful hack and the escape pod launch.
    /// </summary>
    [DataField]
    public TimeSpan LaunchDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Delay after FTL starts before the console sends its private message.
    /// </summary>
    [DataField]
    public TimeSpan MessageDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Delay after FTL starts before the ninja is removed.
    /// </summary>
    [DataField]
    public TimeSpan NinjaRemovalDelay = TimeSpan.FromSeconds(25);

    /// <summary>
    /// Delay after FTL starts before the escape pod grid is removed.
    /// </summary>
    [DataField]
    public TimeSpan PodRemovalDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// FTL travel time used for the fictional Spider Clan route.
    /// </summary>
    [DataField]
    public float FtlTravelTime = 60f;

    /// <summary>
    /// Objective completed by a successful extraction.
    /// </summary>
    [DataField]
    public EntProtoId<ObjectiveComponent> Objective = "SunriseNinjaExtractObjective";
}
