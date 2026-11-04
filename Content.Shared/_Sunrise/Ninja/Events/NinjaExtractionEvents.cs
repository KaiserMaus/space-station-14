using Content.Shared.DoAfter;
using Content.Shared.Objectives.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._Sunrise.Ninja.Events;

/// <summary>
/// Raised after a ninja finishes hacking an escape-pod console.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class NinjaExtractionDoAfterEvent : SimpleDoAfterEvent;

/// <summary>
/// Notifies ninja objective logic about a successful extraction.
/// </summary>
[ByRefEvent]
public readonly record struct NinjaExtractedEvent(EntProtoId<ObjectiveComponent> Objective);
