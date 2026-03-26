namespace Content.Server.Nuke;

public sealed partial class NukeComponent
{
    /// <summary>
    ///     Allows arming/disarming without a disk.
    ///     Anchoring/unanchoring still requires disk.
    /// </summary>
    [DataField]
    [ViewVariables]
    public bool DiskBypassEnabled = false;

    /// <summary>
    ///     Whether to reset disk bypass and timer to defaults after disarm.
    /// </summary>
    [DataField]
    [ViewVariables]
    public bool ShouldResetAfterDiskBypass = false;
}
