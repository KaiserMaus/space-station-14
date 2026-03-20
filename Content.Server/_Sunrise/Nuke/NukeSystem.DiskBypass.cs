namespace Content.Server.Nuke;

public sealed partial class NukeSystem
{
    /// <summary>
    ///     Sets whether the disk is required for arm/disarm and whether this state should reset after disarm.
    /// </summary>
    public void SetDiskBypassEnabled(
        EntityUid uid,
        bool diskBypass,
        bool shouldResetLater = true,
        NukeComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.DiskBypassEnabled = diskBypass;
        component.ShouldResetAfterDiskBypass = shouldResetLater;
        UpdateUserInterface(uid, component);
    }
}
