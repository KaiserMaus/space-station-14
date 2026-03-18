namespace Content.Server._Sunrise.DeviceLinking.Components;

/// <summary>
/// Marks an autolink transmitter that should automatically populate its device list on map init.
/// </summary>
[RegisterComponent]
public sealed partial class AutoLinkToDeviceListComponent : Component
{
    /// <summary>
    /// Whether the matched receivers should be merged into existing entries.
    /// </summary>
    [DataField]
    public bool Merge = true;
}
