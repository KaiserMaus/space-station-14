namespace Content.Server._Sunrise.Research.Events;

/// <summary>
/// Глобально вызывается при запуске необратимого перегрева полноценного исследовательского сервера.
/// </summary>
[ByRefEvent]
public readonly record struct ResearchServerOverheatStartedEvent(EntityUid Server);
