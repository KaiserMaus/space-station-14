using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Pirate;

[RegisterComponent, NetworkedComponent]
public sealed partial class PirateShuttleVoucherComponent : Component
{
    /// <summary>
    /// Grid loaded when this voucher is redeemed.
    /// </summary>
    [DataField(required: true)]
    public ResPath GridPath;

    /// <summary>
    /// Offset from the shipyard console where the shuttle appears.
    /// </summary>
    [DataField]
    public Vector2 Offset = Vector2.Zero;

    /// <summary>
    /// Rotation applied to the spawned shuttle.
    /// </summary>
    [DataField]
    public Angle Rotation = Angle.Zero;

    /// <summary>
    /// Price recorded for sell-back calculations.
    /// </summary>
    [DataField]
    public int Price;

    /// <summary>
    /// Sound played when redeeming the voucher succeeds.
    /// </summary>
    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    /// <summary>
    /// Sound played when redeeming the voucher fails.
    /// </summary>
    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");
}
