using Content.Server.Gatherable.Components;
using Robust.Shared.Random;

namespace Content.Server.Gatherable;

public sealed partial class GatherableSystem
{
    partial void GatherProjectileChance(Entity<GatheringProjectileComponent> gathering, ref bool getChance)
    {
        getChance = _random.Prob(gathering.Comp.Chance);
    }
}
