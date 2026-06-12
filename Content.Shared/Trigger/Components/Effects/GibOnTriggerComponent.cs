using Robust.Shared.GameStates;

namespace Content.Shared.Trigger.Components.Effects;

/// <summary>
/// Will gib the entity when triggered.
/// If TargetUser is true the user will be gibbed instead.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class GibOnTriggerComponent : BaseXOnTriggerComponent
{
    /// <summary>
    /// Should gibbing also delete the owners items?
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DeleteItems = false;

    // Sunrise added start - поддержка растворителя снаряжения без удаления тела
    /// <summary>
    /// Нужно ли гибать саму сущность.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool GibBody = true;

    /// <summary>
    /// Нужно ли создавать гибы при удалении тела.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool GibOrgans = true;
    // Sunrise added end
}
