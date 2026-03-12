using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared._Sunrise.Pirates.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PirateShuttleVoucherComponent : Component
{
    [DataField(required: true)]
    public ResPath GridPath;

    [DataField]
    public Vector2 Offset = Vector2.Zero;

    [DataField]
    public Angle Rotation = Angle.Zero;

    [DataField]
    public int Price;

    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");
}
