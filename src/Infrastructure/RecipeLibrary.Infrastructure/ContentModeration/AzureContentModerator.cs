using Azure;
using Azure.AI.ContentSafety;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.ContentModeration;

public sealed class AzureContentModerator(
    ContentSafetyClient client,
    IOptions<ContentModerationOptions> options,
    ILogger<AzureContentModerator> logger) : IContentModerator
{
    public async Task<ContentModerationResult> ModerateTextAsync(string text, CancellationToken ct = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(text))
        {
            return ContentModerationDecisionMapper.FromSeverities(
                [],
                settings.BlockSeverityThreshold,
                settings.ReviewSeverityThreshold);
        }

        try
        {
            var response = await client.AnalyzeTextAsync(new AnalyzeTextOptions(text), ct);
            var scores = response.Value.CategoriesAnalysis
                .Where(c => c.Severity.HasValue)
                .Select(c => (c.Category.ToString(), c.Severity!.Value));

            return ContentModerationDecisionMapper.FromSeverities(
                scores,
                settings.BlockSeverityThreshold,
                settings.ReviewSeverityThreshold);
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Content Safety text analysis failed.");
            throw new InvalidOperationException("Content moderation service is temporarily unavailable.", ex);
        }
    }

    public async Task<ContentModerationResult> ModerateImageAsync(
        Stream content,
        string contentType,
        CancellationToken ct = default)
    {
        var settings = options.Value;
        ArgumentNullException.ThrowIfNull(content);

        await using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.Length == 0)
        {
            return ContentModerationDecisionMapper.FromSeverities(
                [],
                settings.BlockSeverityThreshold,
                settings.ReviewSeverityThreshold);
        }

        try
        {
            var image = new ContentSafetyImageData(BinaryData.FromBytes(bytes));
            var response = await client.AnalyzeImageAsync(new AnalyzeImageOptions(image), ct);
            var scores = response.Value.CategoriesAnalysis
                .Where(c => c.Severity.HasValue)
                .Select(c => (c.Category.ToString(), c.Severity!.Value));

            return ContentModerationDecisionMapper.FromSeverities(
                scores,
                settings.BlockSeverityThreshold,
                settings.ReviewSeverityThreshold);
        }
        catch (RequestFailedException ex)
        {
            logger.LogError(ex, "Azure Content Safety image analysis failed.");
            throw new InvalidOperationException("Content moderation service is temporarily unavailable.", ex);
        }
    }
}
