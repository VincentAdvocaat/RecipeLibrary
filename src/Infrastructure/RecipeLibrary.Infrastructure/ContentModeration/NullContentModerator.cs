using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Infrastructure.ContentModeration;

/// <summary>No-op moderator used when the feature flag is off or credentials are missing.</summary>
public sealed class NullContentModerator : IContentModerator
{
    public static ContentModerationResult SkippedResult { get; } =
        new(ModerationStatus.NotModerated, 0, [], "skipped", Skipped: true);

    public Task<ContentModerationResult> ModerateTextAsync(string text, CancellationToken ct = default) =>
        Task.FromResult(SkippedResult);

    public Task<ContentModerationResult> ModerateImageAsync(
        Stream content,
        string contentType,
        CancellationToken ct = default) =>
        Task.FromResult(SkippedResult);
}
