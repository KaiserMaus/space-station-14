namespace Content.Server.Nuke;

public sealed partial class NukeComponent
{
    /// <summary>
    ///     Allows arming/disarming without a disk.
    ///     Anchoring/unanchoring still requires disk.
    /// </summary>
    [ViewVariables]
    public bool DiskBypassEnabled = false;

    /// <summary>
    ///     Whether to reset disk bypass and timer to defaults after disarm.
    /// </summary>
    [ViewVariables]
    public bool ShouldResetAfterDiskBypass = false;
}
