namespace Content.Client.Weapons.Ranged.Components;

/// <summary>
/// Настраивает краткую смену спрайта при ручном цикле оружия.
/// </summary>
[RegisterComponent]
public sealed partial class BallisticCycleVisualsComponent : Component
{
    [DataField]
    public string OpenState = "bolt-open";

    [DataField]
    public string ClosedState = "icon";

    [DataField]
    public float AnimationDuration = 0.15f;

    public int LastCycleCount;
}
