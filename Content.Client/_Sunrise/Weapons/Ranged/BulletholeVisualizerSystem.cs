using Content.Shared._Sunrise.Weapons.Ranged;
using Robust.Client.GameObjects;

namespace Content.Client._Sunrise.Weapons.Ranged;

public sealed class BulletholeVisualizerSystem : VisualizerSystem<BulletholeComponent>
{
    private const string DefaultBulletholeRsiPath = "/Textures/_Sunrise/Effects/bulletholes.rsi";

    protected override void OnAppearanceChange(EntityUid uid, BulletholeComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite is not { } sprite)
            return;

        if (!AppearanceSystem.TryGetData<string>(uid, BulletholeVisuals.State, out var state, args.Component))
            return;

        if (!sprite.LayerMapTryGet(BulletholeVisualsLayers.Bullethole, out _))
            sprite.LayerMapReserveBlank(BulletholeVisualsLayers.Bullethole);

        var valid = !string.IsNullOrWhiteSpace(state);
        sprite.LayerSetVisible(BulletholeVisualsLayers.Bullethole, valid);

        if (!valid)
            return;

        string? configuredPath = null;
        AppearanceSystem.TryGetData(uid, BulletholeVisuals.RsiPath, out configuredPath, args.Component);
        var rsiPath = string.IsNullOrWhiteSpace(configuredPath)
            ? DefaultBulletholeRsiPath
            : configuredPath;

        sprite.LayerSetRSI(BulletholeVisualsLayers.Bullethole, rsiPath);
        sprite.LayerSetState(BulletholeVisualsLayers.Bullethole, state);
    }
}
