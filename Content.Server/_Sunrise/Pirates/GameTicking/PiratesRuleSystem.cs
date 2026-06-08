using System.Linq;
using Content.Server.Antag;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Pirate;
using Content.Shared.Cargo.Components;
using Content.Shared.GameTicking.Components;

namespace Content.Server._Sunrise.Pirates.GameTicking;

public sealed class PiratesRuleSystem : GameRuleSystem<PiratesRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PiratesRuleComponent, RuleLoadedGridsEvent>(OnRuleLoadedGrids);
        SubscribeLocalEvent<PirateRoleComponent, GetBriefingEvent>(OnGetBriefing);
        SubscribeLocalEvent<PirateStationComponent, BankBalanceUpdatedEvent>(OnBalanceUpdated);
    }

    protected override void AppendRoundEndText(EntityUid uid,
        PiratesRuleComponent component,
        GameRuleComponent gameRule,
        ref RoundEndTextAppendEvent args)
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

    private void OnGetBriefing(Entity<PirateRoleComponent> role, ref GetBriefingEvent args)
    {
        args.Append(Loc.GetString("pirate-briefing"));
    }

    private void OnRuleLoadedGrids(Entity<PiratesRuleComponent> ent, ref RuleLoadedGridsEvent args)
    {
        foreach (var grid in args.Grids)
        {
            if (!TryComp<PirateBaseComponent>(grid, out var pirateBase))
                continue;

            pirateBase.AssociatedRule = ent.Owner;
            Dirty(grid, pirateBase);

            ent.Comp.AssociatedStation = _station.InitializeNewStation(ent.Comp.StationConfig, [grid]);
            if (TryComp<PirateStationComponent>(ent.Comp.AssociatedStation, out var pirateStation))
            {
                pirateStation.AssociatedRule = GetNetEntity(ent.Owner);
                Dirty(ent.Comp.AssociatedStation, pirateStation);
            }

            EnsureComp<TradeStationComponent>(grid);
        }
    }

    private void OnBalanceUpdated(Entity<PirateStationComponent> ent, ref BankBalanceUpdatedEvent args)
    {
        if (ent.Comp.AssociatedRule is not { } netRule)
            return;

        var ruleUid = GetEntity(netRule);
        if (!TryComp<PiratesRuleComponent>(ruleUid, out var rule) ||
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
}
