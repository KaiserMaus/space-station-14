using System.Numerics;
using Content.Server.GameTicking.Rules;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.Station.Components;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server._Sunrise.Pirates.GameTicking;

public sealed class LoadGridRuleSystem : GameRuleSystem<LoadGridRuleComponent>
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _map = default!;

    private List<Entity<MapGridComponent>> _mapGrids = new();

    protected override void Started(EntityUid uid,
        LoadGridRuleComponent component,
        GameRuleComponent gameRule,
        GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        if (!TryGetRandomStation(out var station) ||
            !TryComp<StationDataComponent>(station, out _))
        {
            Log.Warning("Unable to find a valid station for game rule {RuleId}", args.RuleId);
            ForceEndSelf(uid, gameRule);
            return;
        }

        if (_station.GetLargestGrid(station.Value) is not { } largestGrid)
        {
            Log.Warning("Unable to find a station grid for game rule {RuleId}", args.RuleId);
            ForceEndSelf(uid, gameRule);
            return;
        }

        var mapId = Transform(largestGrid).MapID;
        if (mapId == MapId.Nullspace)
        {
            Log.Warning("Attempted to load grid into nullspace for game rule {RuleId}", args.RuleId);
            ForceEndSelf(uid, gameRule);
            return;
        }

        if (!TryFindFreeOffset(mapId, largestGrid, component, out var offset))
        {
            Log.Warning("Unable to find unobstructed location for game rule {RuleId}", args.RuleId);
            ForceEndSelf(uid, gameRule);
            return;
        }

        if (!_mapLoader.TryLoadGrid(mapId, component.GridPath, out var spawnedGrid, null, offset))
        {
            Log.Warning("Unable to load grid {GridPath} for game rule {RuleId}", component.GridPath, args.RuleId);
            ForceEndSelf(uid, gameRule);
            return;
        }

        var ev = new RuleLoadedGridsEvent(mapId, [spawnedGrid.Value.Owner]);
        RaiseLocalEvent(uid, ref ev);
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
            var currentOffset = stationLocation + RobustRandom.NextVector2(
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
}
