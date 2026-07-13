namespace Content.Server.Antag.Components;

/// <summary>
/// Ghost role spawner that creates an antag for the associated gamerule.
/// </summary>
[RegisterComponent, Access(typeof(AntagSelectionSystem), typeof(Content.Server.Ghost.Roles.GhostRoleSystem))]
public sealed partial class GhostRoleAntagSpawnerComponent : Component
{
    [DataField]
    public EntityUid? Rule;

    [DataField]
    public AntagSelectionDefinition? Definition;
}
