using Robust.Shared.Utility;

namespace Content.Server._Sunrise.Pirates.GameTicking;

[RegisterComponent]
public sealed partial class LoadGridRuleComponent : Component
{
    /// <summary>
    /// Path to the grid that should be loaded near the selected station.
    /// </summary>
    [DataField(required: true)]
    public ResPath GridPath = new();

    /// <summary>
    /// Minimum distance from the station's largest grid.
    /// </summary>
    [DataField]
    public float MinimumDistance = 100f;

    /// <summary>
    /// Maximum distance from the station's largest grid.
    /// </summary>
    [DataField]
    public float MaximumDistance = 1000f;

    /// <summary>
    /// Radius checked for grid collisions before the grid is loaded.
    /// </summary>
    [DataField]
    public float SafetyZoneRadius = 16f;

    /// <summary>
    /// Maximum attempts to find a free location.
    /// </summary>
    [DataField]
    public int MaxAttempts = 100;
}
