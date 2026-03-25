namespace Content.Shared.Paper;

/// <summary>
/// Event fired when using a writing tool on a tamper-proof paper.
/// </summary>
[ByRefEvent]
public record struct PaperSignedEvent(EntityUid Signer, EntityUid Paper);
