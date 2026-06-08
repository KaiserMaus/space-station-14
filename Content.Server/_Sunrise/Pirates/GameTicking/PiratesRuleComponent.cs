using Content.Shared.Cargo.Prototypes;
using Content.Shared.NPC.Prototypes;
using Content.Shared.Station;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Pirates.GameTicking;

[RegisterComponent, Access(typeof(PiratesRuleSystem))]
public sealed partial class PiratesRuleComponent : Component
{
    /// <summary>
    /// Station config applied to the pirate base so it can use cargo and trade systems.
    /// </summary>
    [DataField]
    public StationConfig StationConfig = new()
    {
        StationPrototype = "PirateShuttleStation",
        StationComponentOverrides = new ComponentRegistry(),
    };

    /// <summary>
    /// NPC faction applied to selected pirates.
    /// </summary>
    [DataField]
    public ProtoId<NpcFactionPrototype> Faction = "Pirate";

    /// <summary>
    /// Pirate station entity associated with this rule.
    /// </summary>
    public EntityUid AssociatedStation;

    /// <summary>
    /// Total amount of money collected by pirates during the round.
    /// </summary>
    public int TotalMoneyCollected;

    /// <summary>
    /// Last known balances for pirate station accounts.
    /// </summary>
    public Dictionary<ProtoId<CargoAccountPrototype>, int> LastBalance = new();
}
