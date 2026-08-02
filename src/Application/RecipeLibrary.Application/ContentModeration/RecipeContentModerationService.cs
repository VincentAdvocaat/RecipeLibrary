using System.Text;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.ContentModeration;

/// <summary>
/// Shared moderation orchestration for recipe text and image uploads.
/// </summary>
public sealed class RecipeContentModerationService(
    IContentModerator moderator,
    IContentModerationStore store,
    IUnitOfWork unitOfWork,
    IOptions<ContentModerationOptions> options)
{
    public async Task ApplyTextModerationAsync(
        Recipe recipe,
        CreateRecipeCommand shape,
        CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            // Leave existing status untouched (create defaults to NotModerated; update keeps prior flags).
            return;
        }

        var previousStatus = recipe.ModerationStatus;
        var text = BuildRecipeText(shape);
        var result = await moderator.ModerateTextAsync(text, ct);
        await PersistDecisionAsync(recipe, ContentModerationKind.Text, result, ct);

        // Manual (or prior) rejection must not be silently cleared by a low-severity edit.
        if (previousStatus == ModerationStatus.Rejected
            && recipe.ModerationStatus == ModerationStatus.Approved)
        {
            recipe.ModerationStatus = ModerationStatus.NeedsReview;
            recipe.ModerationSummary = "re-review-after-edit";
            recipe.ModeratedAt = DateTimeOffset.UtcNow;
        }

        await ApplyPendingImageDecisionAsync(recipe, ct);
    }

    /// <summary>
    /// Moderates an image stream. Blocks (throws) on Rejected; returns Approved/NeedsReview for the caller
    /// to persist after storage with a subject key.
    /// </summary>
    public async Task<ContentModerationResult> EnsureImageAllowedAsync(
        Stream content,
        string contentType,
        CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return NullSkippedResult();
        }

        // Buffer so the caller can still read the stream after moderation.
        if (!content.CanSeek)
        {
            throw new InvalidOperationException("Image stream must be seekable for moderation.");
        }

        var position = content.Position;
        var result = await moderator.ModerateImageAsync(content, contentType, ct);
        content.Position = position;

        if (result.Skipped)
        {
            return result;
        }

        if (result.Decision == ModerationStatus.Rejected)
        {
            await store.AddEventAsync(
                new ContentModerationEvent
                {
                    Id = Guid.NewGuid(),
                    RecipeId = null,
                    SubjectKey = null,
                    Kind = ContentModerationKind.Image,
                    Decision = result.Decision,
                    CategoriesSummary = result.Summary,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                ct);
            await unitOfWork.SaveChangesAsync(ct);
            throw new ContentRejectedException();
        }

        return result;
    }

    public async Task RecordImageDecisionAsync(
        string subjectKey,
        ContentModerationResult result,
        CancellationToken ct = default)
    {
        if (result.Skipped || string.IsNullOrWhiteSpace(subjectKey))
        {
            return;
        }

        await store.AddEventAsync(
            new ContentModerationEvent
            {
                Id = Guid.NewGuid(),
                RecipeId = null,
                SubjectKey = subjectKey.Trim(),
                Kind = ContentModerationKind.Image,
                Decision = result.Decision,
                CategoriesSummary = result.Summary,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            ct);
    }

    private async Task ApplyPendingImageDecisionAsync(Recipe recipe, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(recipe.ImageUrl))
        {
            return;
        }

        var subjectKey = recipe.ImageUrl.Trim();
        var imageDecision = await store.GetLatestImageDecisionAsync(subjectKey, ct);
        if (imageDecision is null)
        {
            return;
        }

        await store.AttachImageEventsToRecipeAsync(subjectKey, recipe.Id, ct);

        if (imageDecision == ModerationStatus.NeedsReview
            && recipe.ModerationStatus == ModerationStatus.Approved)
        {
            recipe.ModerationStatus = ModerationStatus.NeedsReview;
            recipe.ModerationSummary = string.IsNullOrWhiteSpace(recipe.ModerationSummary)
                ? "image-needs-review"
                : $"{recipe.ModerationSummary}; image-needs-review";
            recipe.ModeratedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task PersistDecisionAsync(
        Recipe recipe,
        ContentModerationKind kind,
        ContentModerationResult result,
        CancellationToken ct)
    {
        if (result.Skipped)
        {
            return;
        }

        if (result.Decision == ModerationStatus.Rejected)
        {
            await store.AddEventAsync(
                new ContentModerationEvent
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipe.Id == Guid.Empty ? null : recipe.Id,
                    Kind = kind,
                    Decision = result.Decision,
                    CategoriesSummary = result.Summary,
                    CreatedAt = DateTimeOffset.UtcNow,
                },
                ct);
            await unitOfWork.SaveChangesAsync(ct);
            throw new ContentRejectedException();
        }

        recipe.ModerationStatus = result.Decision;
        recipe.ModeratedAt = DateTimeOffset.UtcNow;
        recipe.ModerationSummary = result.Summary;

        await store.AddEventAsync(
            new ContentModerationEvent
            {
                Id = Guid.NewGuid(),
                RecipeId = recipe.Id,
                Kind = kind,
                Decision = result.Decision,
                CategoriesSummary = result.Summary,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            ct);
    }

    public static string BuildRecipeText(CreateRecipeCommand command)
    {
        var sb = new StringBuilder();
        AppendLine(sb, command.Title);
        AppendLine(sb, command.Description);
        foreach (var ingredient in command.Ingredients ?? [])
        {
            AppendLine(sb, ingredient.Name);
            AppendLine(sb, ingredient.Preparation);
        }

        foreach (var step in command.InstructionSteps ?? [])
        {
            AppendLine(sb, step.Text);
        }

        return sb.ToString();
    }

    private static void AppendLine(StringBuilder sb, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            sb.AppendLine(value.Trim());
        }
    }

    private static ContentModerationResult NullSkippedResult() =>
        new(ModerationStatus.NotModerated, 0, [], "skipped", Skipped: true);
}
