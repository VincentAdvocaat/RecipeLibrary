using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Abstractions;

public interface IContentModerationStore
{
    Task AddEventAsync(ContentModerationEvent moderationEvent, CancellationToken ct = default);

    Task AddReportAsync(ContentReport report, CancellationToken ct = default);

    Task<IReadOnlyList<ModerationQueueRecipeItem>> ListNeedsReviewAsync(CancellationToken ct = default);

    Task<IReadOnlyList<ModerationQueueReportItem>> ListOpenReportsAsync(CancellationToken ct = default);

    Task SetRecipeModerationStatusAsync(
        Guid recipeId,
        ModerationStatus status,
        string? summary,
        CancellationToken ct = default);

    Task MarkReportHandledAsync(Guid reportId, CancellationToken ct = default);

    Task<Recipe?> GetRecipeForAdminAsync(Guid recipeId, CancellationToken ct = default);
}
