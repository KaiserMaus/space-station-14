using Content.Server._Sunrise.Objectives.Components;
using Content.Server._Sunrise.Research.Components;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared._Sunrise.Research.Events;
using Content.Shared.Construction.Components;
using Content.Shared.Database;
using Content.Shared.DoAfter;
using Content.Shared.Maps;
using Content.Shared.Mind;
using Content.Shared.Power.Components;
using Content.Shared.Station.Components;
using Content.Shared.Verbs;
using Robust.Shared.Map.Components;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Server._Sunrise.Research.Systems;

/// <summary>
/// Управляет размещением, активацией и обезвреживанием ретранслятора исследовательских данных.
/// </summary>
public sealed partial class ResearchDataSiphonSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedMapSystem _map = default!;
    [Dependency] private SharedMindSystem _mind = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private TurfSystem _turf = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ResearchDataSiphonComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAlternativeVerbs);
        SubscribeLocalEvent<ResearchDataSiphonComponent, AnchorAttemptEvent>(OnAnchorAttempt);
        SubscribeLocalEvent<ResearchDataSiphonComponent, UnanchorAttemptEvent>(OnUnanchorAttempt);
        SubscribeLocalEvent<ResearchDataSiphonComponent, AnchorStateChangedEvent>(OnAnchorStateChanged);
        SubscribeLocalEvent<ResearchDataSiphonComponent, ResearchDataSiphonDeactivateDoAfterEvent>(OnDeactivateDoAfter);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ActiveResearchDataSiphonComponent, ApcPowerReceiverComponent>();
        while (query.MoveNext(out _, out var active, out var power))
        {
            if (power.Powered)
                active.PoweredElapsed += TimeSpan.FromSeconds(frameTime);
        }
    }

    private void OnGetAlternativeVerbs(Entity<ResearchDataSiphonComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        var user = args.User;
        if (HasComp<ActiveResearchDataSiphonComponent>(ent))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Act = () => TryDeactivate(ent.AsNullable(), user),
                Text = Loc.GetString("hijack-beacon-verb-deactivate-text"),
                Message = Loc.GetString("hijack-beacon-verb-deactivate-message"),
                TextStyleClass = "InteractionVerb",
                Impact = LogImpact.High,
            });
            return;
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => TryActivate(ent.AsNullable(), user),
            Text = Loc.GetString("hijack-beacon-verb-activate-text"),
            Message = Loc.GetString("hijack-beacon-verb-activate-message"),
            Disabled = !CanActivate(ent.AsNullable(), user, true),
            TextStyleClass = "InteractionVerb",
            Impact = LogImpact.High,
        });
    }

    private void OnAnchorAttempt(Entity<ResearchDataSiphonComponent> ent, ref AnchorAttemptEvent args)
    {
        if (!CanAnchor(ent))
            args.Cancel();
    }

    private void OnUnanchorAttempt(Entity<ResearchDataSiphonComponent> ent, ref UnanchorAttemptEvent args)
    {
        if (HasComp<ActiveResearchDataSiphonComponent>(ent))
            args.Cancel();
    }

    private void OnAnchorStateChanged(Entity<ResearchDataSiphonComponent> ent, ref AnchorStateChangedEvent args)
    {
        if (!args.Anchored && HasComp<ActiveResearchDataSiphonComponent>(ent))
            Deactivate(ent);
    }

    private void OnDeactivateDoAfter(
        Entity<ResearchDataSiphonComponent> ent,
        ref ResearchDataSiphonDeactivateDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || !HasComp<ActiveResearchDataSiphonComponent>(ent))
            return;

        Deactivate(ent);
        args.Handled = true;
    }

    public bool CanActivate(
        Entity<ResearchDataSiphonComponent?> beacon,
        EntityUid user,
        bool quiet = false)
    {
        if (!Resolve(beacon, ref beacon.Comp) ||
            HasComp<ActiveResearchDataSiphonComponent>(beacon) ||
            HasComp<SpentResearchDataSiphonComponent>(beacon))
        {
            return false;
        }

        if (TryComp<ResearchDataSiphonCooldownComponent>(beacon, out var cooldown) &&
            cooldown.EndsAt > _timing.CurTime)
        {
            return false;
        }

        var xform = Transform(beacon);
        if (!xform.Anchored || !CanPlace(beacon, beacon.Comp, xform))
            return false;

        if (!TryComp<ApcPowerReceiverComponent>(beacon, out var power) || !power.Powered)
            return false;

        return TryGetBinding(beacon.Comp, user, out _, out _);
    }

    public bool TryActivate(Entity<ResearchDataSiphonComponent?> beacon, EntityUid user)
    {
        if (!CanActivate(beacon, user) ||
            !Resolve(beacon, ref beacon.Comp) ||
            !TryGetBinding(beacon.Comp, user, out var mind, out var objective))
        {
            return false;
        }

        RemComp<ResearchDataSiphonCooldownComponent>(beacon);
        beacon.Comp.BoundMind ??= mind;
        beacon.Comp.BoundObjective ??= objective;
        EnsureComp<ActiveResearchDataSiphonComponent>(beacon);
        return true;
    }

    public bool CanDeactivate(
        Entity<ResearchDataSiphonComponent?> beacon,
        EntityUid user,
        bool quiet = false)
    {
        return Resolve(beacon, ref beacon.Comp) &&
               HasComp<ActiveResearchDataSiphonComponent>(beacon);
    }

    public bool TryDeactivate(Entity<ResearchDataSiphonComponent?> beacon, EntityUid user)
    {
        if (!CanDeactivate(beacon, user) || !Resolve(beacon, ref beacon.Comp))
            return false;

        var doAfter = new DoAfterArgs(
            EntityManager,
            user,
            beacon.Comp.DeactivationLength,
            new ResearchDataSiphonDeactivateDoAfterEvent(),
            beacon)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };

        return _doAfter.TryStartDoAfter(doAfter);
    }

    private bool CanAnchor(Entity<ResearchDataSiphonComponent> beacon)
    {
        if (HasComp<SpentResearchDataSiphonComponent>(beacon))
            return false;

        if (TryComp<ResearchDataSiphonCooldownComponent>(beacon, out var cooldown) &&
            cooldown.EndsAt > _timing.CurTime)
        {
            return false;
        }

        return CanPlace(beacon, beacon.Comp, Transform(beacon));
    }

    private bool CanPlace(
        EntityUid beacon,
        ResearchDataSiphonComponent component,
        TransformComponent xform)
    {
        if (xform.GridUid is not { } gridUid ||
            !TryComp<MapGridComponent>(gridUid, out var grid) ||
            !TryComp<StationMemberComponent>(gridUid, out var member) ||
            _station.GetLargestGrid(member.Station) != gridUid)
        {
            return false;
        }

        var worldPosition = _transform.GetWorldPosition(xform);
        var foundCenterFloor = false;
        foreach (var tile in _map.GetTilesIntersecting(
                     gridUid,
                     grid,
                     new Circle(worldPosition, component.PlacementSafetyRadius),
                     false))
        {
            if (tile.GridIndices == _transform.GetGridTilePositionOrDefault((beacon, xform)))
                foundCenterFloor = !_turf.IsSpace(tile);

            if (_turf.IsSpace(tile))
                return false;
        }

        return foundCenterFloor;
    }

    private bool TryGetBinding(
        ResearchDataSiphonComponent component,
        EntityUid user,
        out EntityUid mindUid,
        out EntityUid objectiveUid)
    {
        mindUid = default;
        objectiveUid = default;

        if (!_mind.TryGetMind(user, out var foundMind, out var mind))
            return false;

        mindUid = foundMind;
        if (component.BoundMind is { } boundMind)
        {
            if (boundMind != mindUid ||
                component.BoundObjective is not { } boundObjective ||
                !mind.Objectives.Contains(boundObjective) ||
                !HasComp<StealResearchDataConditionComponent>(boundObjective))
            {
                return false;
            }

            objectiveUid = boundObjective;
            return true;
        }

        foreach (var objective in mind.Objectives)
        {
            if (!HasComp<StealResearchDataConditionComponent>(objective))
                continue;

            objectiveUid = objective;
            return true;
        }

        return false;
    }

    private void Deactivate(Entity<ResearchDataSiphonComponent> beacon)
    {
        RemComp<ActiveResearchDataSiphonComponent>(beacon);
        var cooldown = EnsureComp<ResearchDataSiphonCooldownComponent>(beacon);
        cooldown.EndsAt = _timing.CurTime + beacon.Comp.Cooldown;

        var xform = Transform(beacon);
        if (xform.Anchored)
            _transform.Unanchor(beacon, xform);
    }
}
