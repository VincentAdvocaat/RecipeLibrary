using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Abstractions;

public sealed record ContentModerationCategoryScore(string Category, int Severity);

public sealed record ContentModerationResult(
    ModerationStatus Decision,
    int MaxSeverity,
    IReadOnlyList<ContentModerationCategoryScore> Categories,
    string Summary,
    bool Skipped);

public interface IContentModerator
{
    Task<ContentModerationResult> ModerateTextAsync(string text, CancellationToken ct = default);

    Task<ContentModerationResult> ModerateImageAsync(
        Stream content,
        string contentType,
        CancellationToken ct = default);
}
