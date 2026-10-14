using Content.Client.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client.Weapons.Ranged.Systems;

public sealed partial class BallisticCycleVisualsSystem : VisualizerSystem<BallisticCycleVisualsComponent>
{
    private const string AnimationKey = "ballistic-cycle";

    [Dependency] private AnimationPlayerSystem _animation = default!;

    protected override void OnAppearanceChange(EntityUid uid,
        BallisticCycleVisualsComponent component,
        ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null ||
            !AppearanceSystem.TryGetData<int>(uid, AmmoVisuals.CycleCount, out var cycleCount, args.Component) ||
            cycleCount == component.LastCycleCount ||
            !SpriteSystem.LayerMapTryGet((uid, args.Sprite), GunVisualLayers.Base, out _, false))
        {
            return;
        }

        component.LastCycleCount = cycleCount;

        var player = EnsureComp<AnimationPlayerComponent>(uid);
        if (_animation.HasRunningAnimation(uid, player, AnimationKey))
            _animation.Stop((uid, player), AnimationKey);

        var animation = new Animation
        {
            Length = TimeSpan.FromSeconds(component.AnimationDuration),
            AnimationTracks =
            {
                new AnimationTrackSpriteFlick
                {
                    LayerKey = GunVisualLayers.Base,
                    KeyFrames =
                    {
                        new AnimationTrackSpriteFlick.KeyFrame(component.OpenState, 0f),
                        new AnimationTrackSpriteFlick.KeyFrame(component.ClosedState, component.AnimationDuration),
                    },
                },
            },
        };

        _animation.Play((uid, player), animation, AnimationKey);
    }
}
