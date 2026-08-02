using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Infrastructure.ContentModeration;

internal static class ContentModerationDecisionMapper
{
    public static ContentModerationResult FromSeverities(
        IEnumerable<(string Category, int Severity)> scores,
        int blockThreshold,
        int reviewThreshold,
        bool skipped = false)
    {
        var list = scores
            .Select(s => new ContentModerationCategoryScore(s.Category, s.Severity))
            .OrderByDescending(s => s.Severity)
            .ToList();

        var max = list.Count == 0 ? 0 : list.Max(s => s.Severity);
        var decision = Map(max, blockThreshold, reviewThreshold);
        var summary = list.Count == 0
            ? "none"
            : string.Join(", ", list.Select(s => $"{s.Category}:{s.Severity}"));

        return new ContentModerationResult(decision, max, list, summary, skipped);
    }

    public static ModerationStatus Map(int maxSeverity, int blockThreshold, int reviewThreshold)
    {
        if (maxSeverity >= blockThreshold)
        {
            return ModerationStatus.Rejected;
        }

        if (maxSeverity >= reviewThreshold)
        {
            return ModerationStatus.NeedsReview;
        }

        return ModerationStatus.Approved;
    }
}
