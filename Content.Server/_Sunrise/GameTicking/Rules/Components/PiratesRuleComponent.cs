using Content.Shared.Cargo.Prototypes;
using Content.Shared.Station;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server._Sunrise.GameTicking.Rules.Components;

[RegisterComponent, Access(typeof(PiratesRuleSystem))]
public sealed partial class PiratesRuleComponent : Component
{
    [DataField]
    public StationConfig StationConfig = new()
    {
        StationPrototype = "PirateCoveStation",
        StationComponentOverrides = new ComponentRegistry(),
    };

    [DataField]
    public ResPath CoveGridPath = new("/Maps/_Sunrise/Nonstations/pirate_cove.yml");

    [DataField]
    public float MinimumDistance = 1500f;

    [DataField]
    public float MaximumDistance = 3000f;

    [DataField]
    public EntityUid? AssociatedStation;

    [DataField]
    public EntityUid? TargetStation;

    [DataField]
    public int TotalMoneyCollected;

    [DataField]
    public Dictionary<ProtoId<CargoAccountPrototype>, int> LastBalance = new();
}
