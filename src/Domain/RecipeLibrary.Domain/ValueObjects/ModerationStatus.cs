namespace RecipeLibrary.Domain.ValueObjects;

/// <summary>
/// Moderation outcome for user-generated recipe content.
/// </summary>
public enum ModerationStatus
{
    /// <summary>Moderation was skipped (feature flag off or not yet reviewed).</summary>
    NotModerated = 0,

    /// <summary>Automated (or manual) review approved the content.</summary>
    Approved = 1,

    /// <summary>Severity in the review band; awaiting admin decision.</summary>
    NeedsReview = 2,

    /// <summary>Content blocked or manually rejected.</summary>
    Rejected = 3,
}
