namespace Content.Shared._Sunrise.Weapons.Ranged;

/// <summary>
/// Вызывается направленно на оружии после изменения положения затвора.
/// </summary>
[ByRefEvent]
public readonly record struct BoltClosedChangedEvent(bool Closed);
