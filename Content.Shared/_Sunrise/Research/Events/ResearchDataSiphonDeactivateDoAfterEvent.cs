using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Research.Events;

/// <summary>
/// Завершает обезвреживание ретранслятора исследовательских данных.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class ResearchDataSiphonDeactivateDoAfterEvent : SimpleDoAfterEvent;
