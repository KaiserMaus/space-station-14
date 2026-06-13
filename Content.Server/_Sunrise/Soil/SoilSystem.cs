using Content.Server.Botany.Components;
using Content.Server.Damage.Systems;
using Content.Server.Popups;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;

namespace Content.Server._Sunrise.Soil;

public sealed class SoilSystem : EntitySystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StaminaSystem _stamina = default!;

    private EntityQuery<PlantHolderComponent> _plantHolderQuery;

    public override void Initialize()
    {
        base.Initialize();

        _plantHolderQuery = GetEntityQuery<PlantHolderComponent>();

        SubscribeLocalEvent<SoilComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<SoilComponent> ent, ref UseInHandEvent args)
    {
        var xform = Transform(ent);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
        {
            _popup.PopupEntity(Loc.GetString(ent.Comp.PopupStringFailed), ent, args.User, PopupType.SmallCaution);
            return;
        }

        var tile = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        var coords = _map.ToCenterCoordinates(gridUid, tile, grid);

        foreach (var entity in _lookup.GetEntitiesIntersecting(coords, LookupFlags.Dynamic | LookupFlags.Static))
        {
            if (!_plantHolderQuery.HasComp(entity))
                continue;

            _popup.PopupEntity(Loc.GetString(ent.Comp.PopupStringFailed), ent, args.User, PopupType.SmallCaution);
            return;
        }

        Spawn(ent.Comp.SpawnPrototype, coords);

        var targetMeta = MetaData(ent);
        var userMeta = MetaData(args.User);

        _popup.PopupEntity(Loc.GetString(ent.Comp.PopupStringSuccess, ("user", userMeta.EntityName), ("name", targetMeta.EntityName)), args.User, PopupType.LargeGreen);
        _stamina.TryTakeStamina(args.User, ent.Comp.StaminaDamage);

        QueueDel(ent);
    }
}
