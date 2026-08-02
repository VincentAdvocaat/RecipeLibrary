using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Infrastructure.ContentModeration;

public sealed class EfContentModerationStore(RecipeDbContext dbContext) : IContentModerationStore
{
    public async Task AddEventAsync(ContentModerationEvent moderationEvent, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(moderationEvent);
        await dbContext.ContentModerationEvents.AddAsync(moderationEvent, ct);
    }

    public async Task AddReportAsync(ContentReport report, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        await dbContext.ContentReports.AddAsync(report, ct);
    }

    public async Task<IReadOnlyList<ModerationQueueRecipeItem>> ListNeedsReviewAsync(CancellationToken ct = default)
    {
        var rows = await dbContext.Recipes
            .AsNoTracking()
            .Where(r => r.ModerationStatus == ModerationStatus.NeedsReview)
            .OrderByDescending(r => r.UpdatedAt)
            .Select(r => new
            {
                r.Id,
                r.OwnerUserId,
                Title = r.Title.Value,
                r.ModerationStatus,
                r.ModerationSummary,
                r.ModeratedAt,
                r.UpdatedAt,
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ModerationQueueRecipeItem(
                r.Id,
                r.OwnerUserId,
                r.Title,
                r.ModerationStatus.ToString(),
                r.ModerationSummary,
                r.ModeratedAt,
                r.UpdatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<ModerationQueueReportItem>> ListOpenReportsAsync(CancellationToken ct = default)
    {
        return await (
                from report in dbContext.ContentReports.AsNoTracking()
                join recipe in dbContext.Recipes.AsNoTracking() on report.RecipeId equals recipe.Id
                where !report.Handled
                orderby report.CreatedAt descending
                select new ModerationQueueReportItem(
                    report.Id,
                    report.RecipeId,
                    recipe.Title.Value,
                    report.ReporterUserId,
                    report.Reason,
                    report.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task SetRecipeModerationStatusAsync(
        Guid recipeId,
        ModerationStatus status,
        string? summary,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await dbContext.Recipes
            .Where(r => r.Id == recipeId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.ModerationStatus, status)
                    .SetProperty(r => r.ModerationSummary, summary)
                    .SetProperty(r => r.ModeratedAt, now)
                    .SetProperty(r => r.UpdatedAt, now),
                ct);
    }

    public async Task MarkReportHandledAsync(Guid reportId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        await dbContext.ContentReports
            .Where(r => r.Id == reportId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(r => r.Handled, true)
                    .SetProperty(r => r.HandledAt, now),
                ct);
    }

    public Task<Recipe?> GetRecipeForAdminAsync(Guid recipeId, CancellationToken ct = default) =>
        dbContext.Recipes
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == recipeId, ct);

    public async Task<ModerationStatus?> GetLatestImageDecisionAsync(string subjectKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subjectKey))
        {
            return null;
        }

        var key = subjectKey.Trim();
        return await dbContext.ContentModerationEvents
            .AsNoTracking()
            .Where(e => e.Kind == ContentModerationKind.Image && e.SubjectKey == key)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => (ModerationStatus?)e.Decision)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AttachImageEventsToRecipeAsync(string subjectKey, Guid recipeId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(subjectKey) || recipeId == Guid.Empty)
        {
            return;
        }

        var key = subjectKey.Trim();
        await dbContext.ContentModerationEvents
            .Where(e => e.Kind == ContentModerationKind.Image && e.SubjectKey == key && e.RecipeId == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(e => e.RecipeId, recipeId),
                ct);
    }
}
