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
            recipe.ModerationStatus = ModerationStatus.NotModerated;
            recipe.ModeratedAt = null;
            recipe.ModerationSummary = null;
            return;
        }

        var text = BuildRecipeText(shape);
        var result = await moderator.ModerateTextAsync(text, ct);
        await PersistDecisionAsync(recipe, ContentModerationKind.Text, result, ct);
    }

    public async Task EnsureImageAllowedAsync(Stream content, string contentType, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
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
            return;
        }

        await store.AddEventAsync(
            new ContentModerationEvent
            {
                Id = Guid.NewGuid(),
                RecipeId = null,
                Kind = ContentModerationKind.Image,
                Decision = result.Decision,
                CategoriesSummary = result.Summary,
                CreatedAt = DateTimeOffset.UtcNow,
            },
            ct);

        if (result.Decision == ModerationStatus.Rejected)
        {
            await unitOfWork.SaveChangesAsync(ct);
            throw new ContentRejectedException();
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
            recipe.ModerationStatus = ModerationStatus.NotModerated;
            recipe.ModeratedAt = null;
            recipe.ModerationSummary = null;
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
}
