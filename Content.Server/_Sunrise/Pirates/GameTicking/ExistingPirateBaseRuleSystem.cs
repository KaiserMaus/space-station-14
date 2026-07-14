using Content.Server.Antag;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Pirate;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;

namespace Content.Server._Sunrise.Pirates.GameTicking;

public sealed class ExistingPirateBaseRuleSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExistingPirateBaseRuleComponent, GameRuleStartedEvent>(
            OnGameRuleStarted,
            before: [typeof(AntagSelectionSystem)]);
    }

    private void OnGameRuleStarted(Entity<ExistingPirateBaseRuleComponent> ent, ref GameRuleStartedEvent args)
    {
        if (!TryComp<GameRuleComponent>(ent, out var gameRule))
            return;

        if (!TryComp<PiratesRuleComponent>(ent, out var piratesRule))
        {
            ForceEndSelf(ent, gameRule);
            return;
        }

        if (!TryFindPirateBase(out var pirateBaseUid, out var pirateBase))
        {
            Log.Warning("Unable to find an existing pirate base for game rule {RuleId}", args.RuleId);
            ForceEndSelf(ent, gameRule);
            return;
        }

        EnsurePirateStation(ent, piratesRule, (pirateBaseUid, pirateBase));

        var ev = new RuleLoadedGridsEvent(Transform(pirateBaseUid).MapID, [pirateBaseUid]);
        RaiseLocalEvent(ent, ref ev);
    }

    private bool TryFindPirateBase(out EntityUid uid, out PirateBaseComponent component)
    {
        var query = EntityQueryEnumerator<PirateBaseComponent, TransformComponent>();
        while (query.MoveNext(out var baseUid, out var pirateBase, out var xform))
        {
            if (TerminatingOrDeleted(baseUid))
                continue;

            if (xform.MapID == Robust.Shared.Map.MapId.Nullspace)
                continue;

            uid = baseUid;
            component = pirateBase;
            return true;
        }

        uid = EntityUid.Invalid;
        component = default!;
        return false;
    }

    private void EnsurePirateStation(
        Entity<ExistingPirateBaseRuleComponent> rule,
        PiratesRuleComponent piratesRule,
        Entity<PirateBaseComponent> pirateBase)
    {
        if (TryGetExistingPirateStation(pirateBase.Comp.AssociatedRule, out var stationUid))
        {
            piratesRule.AssociatedStation = stationUid;
        }
        else
        {
            piratesRule.AssociatedStation = _station.InitializeNewStation(piratesRule.StationConfig, [pirateBase.Owner]);
            if (TryComp<PirateStationComponent>(piratesRule.AssociatedStation, out var pirateStation))
            {
                pirateStation.AssociatedRule = GetNetEntity(rule.Owner);
                Dirty(piratesRule.AssociatedStation, pirateStation);
            }
        }

        pirateBase.Comp.AssociatedRule = rule.Owner;
        Dirty(pirateBase);
        EnsureComp<TradeStationComponent>(pirateBase);
    }

    private bool TryGetExistingPirateStation(
        EntityUid ruleUid,
        out EntityUid stationUid)
    {
        stationUid = EntityUid.Invalid;

        if (!Exists(ruleUid) ||
            !TryComp<PiratesRuleComponent>(ruleUid, out var piratesRule) ||
            piratesRule.AssociatedStation == EntityUid.Invalid ||
            !HasComp<PirateStationComponent>(piratesRule.AssociatedStation))
        {
            return false;
        }

        stationUid = piratesRule.AssociatedStation;
        return true;
    }

    private void ForceEndSelf(EntityUid uid, GameRuleComponent gameRule)
    {
        _gameTicker.EndGameRule(uid, gameRule);
    }
}
