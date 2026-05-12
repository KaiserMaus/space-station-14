using Content.Shared.Silicons.Borgs.Components;

namespace Content.Shared._Sunrise.Silicons.Borgs.Events;

/// <summary>
/// Raised after module-provided items are enabled or disabled for a borg module.
/// </summary>
[ByRefEvent]
public record struct BorgModuleItemsToggledEvent(EntityUid Chassis, bool Enabled);
