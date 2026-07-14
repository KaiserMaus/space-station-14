using System.Numerics;
using Content.Server._Sunrise.Helpers;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server._Sunrise.Pirates.GameTicking;

public sealed class LoadGridRuleSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly SunriseHelpersSystem _helpers = default!;
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private List<Entity<MapGridComponent>> _mapGrids = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LoadGridRuleComponent, GameRuleStartedEvent>(
            OnGameRuleStarted,
            before: [typeof(AntagSelectionSystem)]);
    }

    private void OnGameRuleStarted(Entity<LoadGridRuleComponent> ent, ref GameRuleStartedEvent args)
    {
        if (!TryComp<GameRuleComponent>(ent, out var gameRule))
            return;

        LoadGrid(ent, gameRule, args.RuleId);
    }

    private void LoadGrid(Entity<LoadGridRuleComponent> ent, GameRuleComponent gameRule, string ruleId)
    {
        if (!_helpers.TryGetRandomStation(out var station) ||
            !TryComp<StationDataComponent>(station, out _))
        {
            Log.Warning("Unable to find a valid station for game rule {RuleId}", ruleId);
            ForceEndSelf(ent, gameRule);
            return;
        }

        if (_station.GetLargestGrid(station.Value) is not { } largestGrid)
        {
            Log.Warning("Unable to find a station grid for game rule {RuleId}", ruleId);
            ForceEndSelf(ent, gameRule);
            return;
        }

        var mapId = Transform(largestGrid).MapID;
        if (mapId == MapId.Nullspace)
        {
            Log.Warning("Attempted to load grid into nullspace for game rule {RuleId}", ruleId);
            ForceEndSelf(ent, gameRule);
            return;
        }

        if (!TryFindFreeOffset(mapId, largestGrid, ent.Comp, out var offset))
        {
            Log.Warning("Unable to find unobstructed location for game rule {RuleId}", ruleId);
            ForceEndSelf(ent, gameRule);
            return;
        }

        if (!_mapLoader.TryLoadGrid(mapId, ent.Comp.GridPath, out var spawnedGrid, null, offset))
        {
            Log.Warning("Unable to load grid {GridPath} for game rule {RuleId}", ent.Comp.GridPath, ruleId);
            ForceEndSelf(ent, gameRule);
            return;
        }

        var ev = new RuleLoadedGridsEvent(mapId, [spawnedGrid.Value.Owner]);
        RaiseLocalEvent(ent, ref ev);
    }

    private bool TryFindFreeOffset(
        MapId mapId,
        EntityUid stationGrid,
        LoadGridRuleComponent component,
        out Vector2 offset)
    {
        var stationLocation = _transform.GetWorldPosition(stationGrid);

        for (var i = 0; i < component.MaxAttempts; i++)
        {
            var currentOffset = stationLocation + _random.NextVector2(
                component.MinimumDistance,
                component.MaximumDistance);

            var safetyBounds = Box2.UnitCentered.Enlarged(component.SafetyZoneRadius).Translated(currentOffset);
            if (HasCollisions(mapId, safetyBounds))
                continue;

            offset = currentOffset;
            return true;
        }

        offset = Vector2.Zero;
        return false;
    }

    private bool HasCollisions(MapId mapId, Box2 bounds)
    {
        _mapGrids.Clear();
        _map.FindGridsIntersecting(mapId, bounds, ref _mapGrids);
        return _mapGrids.Count > 0;
    }

    private void ForceEndSelf(EntityUid uid, GameRuleComponent gameRule)
    {
        _gameTicker.EndGameRule(uid, gameRule);
    }
}
