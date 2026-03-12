using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Pirates.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class PirateShipyardConsoleComponent : Component
{
    [DataField]
    public float SellRate = 0.7f;

    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");
}
