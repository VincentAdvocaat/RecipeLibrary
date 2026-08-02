using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.ContentModeration;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Infrastructure.ContentModeration;

namespace RecipeLibrary.Application.Tests;

internal static class TestContentModeration
{
    public static RecipeContentModerationService Disabled(
        IUnitOfWork? unitOfWork = null,
        FakeContentModerationStore? store = null) =>
        Create(enabled: false, moderator: new NullContentModerator(), unitOfWork, store);

    public static RecipeContentModerationService WithModerator(
        IContentModerator moderator,
        bool enabled = true,
        IUnitOfWork? unitOfWork = null,
        FakeContentModerationStore? store = null) =>
        Create(enabled, moderator, unitOfWork, store);

    private static RecipeContentModerationService Create(
        bool enabled,
        IContentModerator moderator,
        IUnitOfWork? unitOfWork,
        FakeContentModerationStore? store)
    {
        var options = Options.Create(new ContentModerationOptions
        {
            Enabled = enabled,
            BlockSeverityThreshold = 4,
            ReviewSeverityThreshold = 2,
        });

        return new RecipeContentModerationService(
            moderator,
            store ?? new FakeContentModerationStore(),
            unitOfWork ?? new NoOpUnitOfWork(),
            options);
    }

    internal sealed class FakeContentModerationStore : IContentModerationStore
    {
        public List<ContentModerationEvent> Events { get; } = [];
        public List<ContentReport> Reports { get; } = [];

        public Task AddEventAsync(ContentModerationEvent moderationEvent, CancellationToken ct = default)
        {
            Events.Add(moderationEvent);
            return Task.CompletedTask;
        }

        public Task AddReportAsync(ContentReport report, CancellationToken ct = default)
        {
            Reports.Add(report);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ModerationQueueRecipeItem>> ListNeedsReviewAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ModerationQueueRecipeItem>>([]);

        public Task<IReadOnlyList<ModerationQueueReportItem>> ListOpenReportsAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ModerationQueueReportItem>>([]);

        public Task SetRecipeModerationStatusAsync(
            Guid recipeId,
            ModerationStatus status,
            string? summary,
            CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task MarkReportHandledAsync(Guid reportId, CancellationToken ct = default) => Task.CompletedTask;

        public Task<Recipe?> GetRecipeForAdminAsync(Guid recipeId, CancellationToken ct = default) =>
            Task.FromResult<Recipe?>(null);

        public Task<ModerationStatus?> GetLatestImageDecisionAsync(string subjectKey, CancellationToken ct = default)
        {
            var match = Events
                .Where(e => e.Kind == ContentModerationKind.Image
                    && string.Equals(e.SubjectKey, subjectKey, StringComparison.Ordinal))
                .OrderByDescending(e => e.CreatedAt)
                .FirstOrDefault();
            return Task.FromResult(match?.Decision);
        }

        public Task AttachImageEventsToRecipeAsync(string subjectKey, Guid recipeId, CancellationToken ct = default)
        {
            foreach (var e in Events.Where(e =>
                         e.Kind == ContentModerationKind.Image
                         && string.Equals(e.SubjectKey, subjectKey, StringComparison.Ordinal)
                         && e.RecipeId is null))
            {
                e.RecipeId = recipeId;
            }

            return Task.CompletedTask;
        }
    }

    internal sealed class FakeContentModerator(ContentModerationResult result) : IContentModerator
    {
        public Task<ContentModerationResult> ModerateTextAsync(string text, CancellationToken ct = default) =>
            Task.FromResult(result);

        public Task<ContentModerationResult> ModerateImageAsync(
            Stream content,
            string contentType,
            CancellationToken ct = default) =>
            Task.FromResult(result);
    }
}
