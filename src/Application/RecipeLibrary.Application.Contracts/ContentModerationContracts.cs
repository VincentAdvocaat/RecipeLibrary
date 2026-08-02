namespace RecipeLibrary.Application.Contracts;

public sealed class GetModerationQueueQuery : IQuery<ModerationQueueResult>;

public sealed record ModerationQueueResult(
    IReadOnlyList<ModerationQueueRecipeItem> NeedsReview,
    IReadOnlyList<ModerationQueueReportItem> OpenReports);

public sealed record ModerationQueueRecipeItem(
    Guid RecipeId,
    string OwnerUserId,
    string Title,
    string Status,
    string? Summary,
    DateTimeOffset? ModeratedAt,
    DateTimeOffset UpdatedAt);

public sealed record ModerationQueueReportItem(
    Guid ReportId,
    Guid RecipeId,
    string RecipeTitle,
    string ReporterUserId,
    string? Reason,
    DateTimeOffset CreatedAt);

public sealed class SetRecipeModerationDecisionCommand : ICommand<SetRecipeModerationDecisionResult>
{
    public required Guid RecipeId { get; init; }

    /// <summary>Domain <c>ModerationStatus</c> name: Approved or Rejected.</summary>
    public required string Decision { get; init; }

    public Guid? RelatedReportId { get; init; }
}

public sealed record SetRecipeModerationDecisionResult(Guid RecipeId, string Decision);

public sealed class ReportRecipeContentCommand : ICommand<ReportRecipeContentResult>
{
    public required Guid RecipeId { get; init; }

    public string? Reason { get; init; }
}

public sealed record ReportRecipeContentResult(Guid ReportId);
