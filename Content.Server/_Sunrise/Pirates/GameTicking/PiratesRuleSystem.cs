using System.Linq;
using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Server.Cargo.Components;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.RandomMetadata;
using Content.Server.Roles;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Pirate;
using Content.Shared.Cargo.Components;
using Content.Shared.Clothing;
using Content.Shared.Dataset;
using Content.Shared.GameTicking.Components;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._Sunrise.Pirates.GameTicking;

public sealed class PiratesRuleSystem : GameRuleSystem<PiratesRuleComponent>
{
    [Dependency] private readonly AntagSelectionSystem _antag = default!;
    [Dependency] private readonly LoadoutSystem _loadout = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly RandomMetadataSystem _randomMetadata = default!;
    [Dependency] private readonly StationSystem _station = default!;

    private const int DeepSpaceOnlineThreshold = 50;
    private const int ScrapperOnlineThreshold = 80;
    private const string PirateCaptainMindRole = "MindRolePirateCaptain";
    private const string PirateCaptainNameFormat = "name-format-pirate-captain";
    private const string PirateCrewNameFormat = "name-format-pirate-crew";

    private static readonly ProtoId<LocalizedDatasetPrototype> PirateNameDataset = "NamesDeathCommando";

    private static readonly List<ProtoId<LocalizedDatasetPrototype>> PirateNameSegments = [PirateNameDataset];
    private static readonly List<ProtoId<RoleLoadoutPrototype>> PirateRoleLoadout = ["RoleSurvivalEVA"];

    private static readonly List<ProtoId<StartingGearPrototype>> ScoonerCrewGear =
    [
        "SunrisePirateScoonerAltA",
        "SunrisePirateScoonerAltB",
        "SunrisePirateScoonerAltC",
        "SunrisePirateScoonerAltD"
    ];

    private static readonly List<ProtoId<StartingGearPrototype>> DeepSpaceCrewGear =
    [
        "SunrisePirateDeepSpaceAltA",
        "SunrisePirateDeepSpaceAltB",
        "SunrisePirateDeepSpaceAltC",
        "SunrisePirateDeepSpaceAltD"
    ];

    private static readonly List<ProtoId<StartingGearPrototype>> ScrapperCrewGear =
    [
        "SunrisePirateScrapperAltA",
        "SunrisePirateScrapperAltB",
        "SunrisePirateScrapperAltC",
        "SunrisePirateScrapperAltD"
    ];

    private static readonly List<ProtoId<StartingGearPrototype>> ScoonerCaptainGear = ["SunrisePirateCaptainScooner"];

    private static readonly List<ProtoId<StartingGearPrototype>> DeepSpaceCaptainGear =
    [
        "SunrisePirateCaptainDeepSpace",
        "SunrisePirateCaptainDeepSpaceAltB"
    ];

    private static readonly List<ProtoId<StartingGearPrototype>> ScrapperCaptainGear =
    [
        "SunrisePirateCaptainScrapper",
        "SunrisePirateCaptainScrapperAltB"
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PiratesRuleComponent, AfterAntagEntitySelectedEvent>(OnAfterAntagEntitySelected);
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

    private void OnAfterAntagEntitySelected(Entity<PiratesRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        var playerCount = _antag.GetTotalPlayerCount(_player.Sessions);
        var isCaptain = HasMindRole(args.Def, PirateCaptainMindRole);

        _loadout.Equip(args.EntityUid, GetGear(playerCount, isCaptain), PirateRoleLoadout);

        var name = GenerateRandomPirateName(isCaptain);
        _meta.SetEntityName(args.EntityUid, name, MetaData(args.EntityUid));

        var assignedMinds = args.GameRule.Comp.AssignedMinds;
        if (assignedMinds.Count != 0)
        {
            var lastIndex = assignedMinds.Count - 1;
            var (mind, _) = assignedMinds[lastIndex];
            assignedMinds[lastIndex] = (mind, name);
        }
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

    private static List<ProtoId<StartingGearPrototype>> GetGear(int playerCount, bool isCaptain)
    {
        if (playerCount >= ScrapperOnlineThreshold)
            return isCaptain ? ScrapperCaptainGear : ScrapperCrewGear;

        if (playerCount >= DeepSpaceOnlineThreshold)
            return isCaptain ? DeepSpaceCaptainGear : DeepSpaceCrewGear;

        return isCaptain ? ScoonerCaptainGear : ScoonerCrewGear;
    }

    private string GenerateRandomPirateName(bool isCaptain)
    {
        var loc = isCaptain
            ? PirateCaptainNameFormat
            : PirateCrewNameFormat;

        return _randomMetadata.GetRandomFromSegments(PirateNameSegments, loc);
    }

    private static bool HasMindRole(AntagSelectionDefinition def, string roleId)
    {
        if (def.MindRoles == null)
            return false;

        foreach (var role in def.MindRoles)
        {
            if (role.Id == roleId)
                return true;
        }

        return false;
    }
}
