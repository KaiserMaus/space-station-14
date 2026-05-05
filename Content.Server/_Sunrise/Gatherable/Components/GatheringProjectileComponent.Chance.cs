#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Content.Server.Gatherable.Components;

public sealed partial class GatheringProjectileComponent
{
    /// <summary>
    /// Chance to gather on collision. 1.0 = 100%.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public float Chance = 1f;
}
