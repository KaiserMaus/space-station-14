using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared._Sunrise.Magic.InfiniteGuns;

public sealed partial class InfiniteGunsActionEvent : InstantActionEvent
{
    [DataField(required: true)]
    public EntProtoId WeaponPrototype;

    [DataField]
    public int SpawnCount = 31;

    [DataField]
    public int BatchSize = 1;
}
