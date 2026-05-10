using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    private static readonly EntProtoId CargoPalletTeleportEffectPrototype = "SunriseCargoPalletTeleportEffect";

    private void SpawnCargoPalletTeleportEffect(EntityCoordinates coordinates)
    {
        Spawn(CargoPalletTeleportEffectPrototype, coordinates);
    }
}
