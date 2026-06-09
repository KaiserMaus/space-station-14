using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._Sunrise.Pirate;

[RegisterComponent, NetworkedComponent]
public sealed partial class PirateShipyardConsoleComponent : Component
{
    /// <summary>
    /// Fraction of the purchased shuttle price returned when the shuttle is sold back.
    /// </summary>
    [DataField]
    public float SellRate = 0.7f;

    /// <summary>
    /// Sound played when an operation succeeds.
    /// </summary>
    [DataField]
    public SoundSpecifier ConfirmSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");

    /// <summary>
    /// Sound played when an operation fails.
    /// </summary>
    [DataField]
    public SoundSpecifier ErrorSound = new SoundPathSpecifier("/Audio/Machines/terminal_insert_disc.ogg");
}
