using System.Numerics;
using System.Linq;
using Content.Server._Sunrise.GameTicking.Rules.Components;
using Content.Server._Sunrise.Pirates;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Pirates.Components;
using Content.Shared.Cargo.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.EntitySerialization;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.GameTicking.Rules;

public sealed class PiratesRuleSystem : GameRuleSystem<PiratesRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PiratesRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);
        SubscribeLocalEvent<PirateStationComponent, BankBalanceUpdatedEvent>(OnBalanceUpdated);
        SubscribeLocalEvent<PirateRoleComponent, GetBriefingEvent>(OnGetBriefing);
    }

    protected override void Added(EntityUid uid, PiratesRuleComponent component, GameRuleComponent gameRule, GameRuleAddedEvent args)
    {
        if (!TryGetRandomStation(out var targetStation))
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        component.TargetStation = targetStation;

        if (!TryComp<StationDataComponent>(targetStation, out var stationData) ||
            stationData.Grids.Count == 0)
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        var mapId = Transform(stationData.Grids.First()).MapID;
        var offset = GetSpawnOffset(component.MinimumDistance, component.MaximumDistance);
        var stationArea = GetStationArea(targetStation.Value);
        var loadOptions = DeserializationOptions.Default with { InitializeMaps = true };

        if (!_mapLoader.TryLoadGrid(mapId, component.CoveGridPath, out var grid, loadOptions, stationArea.Center + offset))
        {
            ForceEndSelf(uid, gameRule);
            return;
        }

        var grids = new List<EntityUid> { grid.Value.Owner };
        var ev = new RuleLoadedGridsEvent(mapId, grids);
        RaiseLocalEvent(uid, ref ev);
    }

    private void OnGetBriefing(Entity<PirateRoleComponent> ent, ref GetBriefingEvent args)
    {
        args.Append(Loc.GetString("pirate-briefing"));
    }

    private void OnRuleLoadedGrids(Entity<PiratesRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        foreach (var grid in args.Grids)
        {
            if (!TryComp<PirateBaseComponent>(grid, out var baseComp))
                continue;

            baseComp.AssociatedRule = GetNetEntity(ent.Owner);
            Dirty(grid, baseComp);

            ent.Comp.AssociatedStation = _station.InitializeNewStation(ent.Comp.StationConfig, [grid]);

            if (!TryComp<PirateStationComponent>(ent.Comp.AssociatedStation, out var stationComp))
                continue;

            stationComp.AssociatedRule = GetNetEntity(ent.Owner);
            Dirty(ent.Comp.AssociatedStation.Value, stationComp);
        }
    }

    private void OnBalanceUpdated(Entity<PirateStationComponent> ent, ref BankBalanceUpdatedEvent args)
    {
        if (ent.Comp.AssociatedRule is not { } ruleNet ||
            !TryComp<PiratesRuleComponent>(GetEntity(ruleNet), out var rule) ||
            rule.AssociatedStation != ent.Owner)
            return;

        var moneyEarned = 0;
        foreach (var (account, balance) in args.Balance)
        {
            if (!rule.LastBalance.TryGetValue(account, out var lastBalance))
                continue;

            var transaction = balance - lastBalance;
            if (transaction > 0)
                moneyEarned += transaction;
        }

        rule.LastBalance = args.Balance.ToDictionary();
        rule.TotalMoneyCollected += moneyEarned;
    }

    protected override void AppendRoundEndText(EntityUid uid, PiratesRuleComponent component, GameRuleComponent gameRule, ref RoundEndTextAppendEvent args)
    {
        args.AddLine(Loc.GetString("pirates-existing"));
        args.AddLine(Loc.GetString("pirates-earned-spesos", ("money", component.TotalMoneyCollected)));
        args.AddLine(Loc.GetString("pirate-list-start"));

        var antags = _antag.GetAntagIdentifiers(uid);
        foreach (var (_, sessionData, name) in antags)
        {
            args.AddLine(Loc.GetString("pirate-list-name-user", ("name", name), ("user", sessionData.UserName)));
        }
    }

    private Vector2 GetSpawnOffset(float minDistance, float maxDistance)
    {
        var angle = _random.NextAngle();
        var distance = _random.NextFloat(minDistance, maxDistance);
        return angle.ToWorldVec() * distance;
    }

    private Box2 GetStationArea(EntityUid targetStation)
    {
        var grids = Comp<StationDataComponent>(targetStation).Grids;
        var boxes = grids
            .Where(HasComp<MapGridComponent>)
            .Select(grid => Transform(grid).WorldMatrix.TransformBox(Comp<MapGridComponent>(grid).LocalAABB))
            .ToArray();

        var stationArea = boxes[0];
        for (var i = 1; i < boxes.Length; i++)
        {
            stationArea.Union(boxes[i]);
        }

        return stationArea;
    }
}
