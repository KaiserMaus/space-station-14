namespace Content.Server._Sunrise.Ninja.Components;

/// <summary>
/// Stores the active escape-pod extraction timeline.
/// </summary>
[RegisterComponent]
public sealed partial class NinjaEscapePodExtractionComponent : Component
{
    /// <summary>
    /// Ninja being extracted by this pod.
    /// </summary>
    public EntityUid Ninja;

    /// <summary>
    /// Console used to start the extraction.
    /// </summary>
    public EntityUid Console;

    /// <summary>
    /// Absolute time at which the pod should begin launching.
    /// </summary>
    public TimeSpan LaunchAt;

    /// <summary>
    /// FTL travel time, deliberately longer than the extraction timeline.
    /// </summary>
    public float FtlTravelTime;

    /// <summary>
    /// Delay from FTL start to the console message.
    /// </summary>
    public TimeSpan MessageDelay;

    /// <summary>
    /// Delay from FTL start to ninja removal.
    /// </summary>
    public TimeSpan NinjaRemovalDelay;

    /// <summary>
    /// Delay from FTL start to pod removal.
    /// </summary>
    public TimeSpan PodRemovalDelay;

    /// <summary>
    /// Actual time at which FTL began.
    /// </summary>
    public TimeSpan? FtlStartedAt;

    /// <summary>
    /// Absolute deadline for the console message.
    /// </summary>
    public TimeSpan? MessageAt;

    /// <summary>
    /// Absolute deadline for ninja removal.
    /// </summary>
    public TimeSpan? NinjaRemovalAt;

    /// <summary>
    /// Absolute deadline for pod removal.
    /// </summary>
    public TimeSpan? PodRemovalAt;

    /// <summary>
    /// Whether the system has already attempted to launch this pod.
    /// </summary>
    public bool LaunchAttempted;

    /// <summary>
    /// Whether the console message has been processed.
    /// </summary>
    public bool MessageSent;

    /// <summary>
    /// Whether the ninja removal stage has been processed.
    /// </summary>
    public bool NinjaRemoved;
}
