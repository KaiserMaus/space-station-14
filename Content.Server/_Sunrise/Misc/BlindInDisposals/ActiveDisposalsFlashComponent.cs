namespace Content.Server._Sunrise.Misc.BlindInDisposals;

[RegisterComponent]
public sealed partial class ActiveDisposalsFlashComponent : Component
{
    /// <summary>
    /// Next time this entity should have its flash status refreshed while inside disposal tubes.
    /// </summary>
    public TimeSpan NextFlashTime;
}
