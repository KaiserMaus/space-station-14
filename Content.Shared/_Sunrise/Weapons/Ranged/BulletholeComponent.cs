using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Weapons.Ranged;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(BulletholeSystem))]
public sealed partial class BulletholeComponent : Component
{
    [DataField, AutoNetworkedField]
    public int BulletholeCount;

    [DataField, AutoNetworkedField]
    public int BulletholeState;
}

[RegisterComponent, NetworkedComponent]
[Access(typeof(BulletholeSystem))]
public sealed partial class BulletholeGeneratorComponent : Component
{
    [DataField("rsiPath")]
    public string RsiPath = "/Textures/_Sunrise/Effects/bulletholes.rsi";
}

[Serializable, NetSerializable]
public enum BulletholeVisuals : byte
{
    State,
    RsiPath,
}

[Serializable, NetSerializable]
public enum BulletholeVisualsLayers : byte
{
    Bullethole,
}
