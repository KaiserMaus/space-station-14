using Content.Shared.Emag.Systems;

namespace Content.Shared.Delivery;

public abstract partial class SharedDeliverySystem
{
    [Dependency] private readonly EmagSystem _emag = default!;

    private void OnSpawnerEmagged(Entity<DeliverySpawnerComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Interaction))
            return;

        if (_emag.CheckFlag(ent, EmagType.Interaction))
            return;

        args.Handled = true;
    }
}
