using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;

namespace RecipeLibrary.Application.RecipeImport;

/// <summary>
/// Import entry: normalize any modality to plain text, then parse deterministically with optional AI assist.
/// </summary>
public sealed class RecipeImportService(
    RecipeTextParser recipeTextParser,
    HtmlRecipeTextExtractor htmlRecipeTextExtractor,
    IngredientMatcher ingredientMatcher,
    IRecipeAiParser recipeAiParser,
    IOptions<RecipeImportOptions> importOptions)
{
    public async Task<ImportRecipeResult> ImportContentAsync(
        ImportRecipeContentQuery query,
        CancellationToken ct = default)
    {
        var text = ResolvePlainText(query.Content, query.ContentKind);
        return await BuildResultAsync(text, query.ParseOptions, ct);
    }

    public async Task<ImportRecipeResult> ImportPlainTextAsync(
        string plainText,
        ImportRecipeParseOptions? parseOptions = null,
        CancellationToken ct = default) =>
        await BuildResultAsync(plainText ?? string.Empty, parseOptions, ct);

    public string HtmlToRecipeText(string html) => htmlRecipeTextExtractor.Extract(html);

    private string ResolvePlainText(string content, ImportContentKind contentKind)
    {
        var raw = content ?? string.Empty;
        var isHtml = contentKind switch
        {
            ImportContentKind.Html => true,
            ImportContentKind.PlainText => false,
            _ => LooksLikeHtml(raw),
        };

        return isHtml ? htmlRecipeTextExtractor.Extract(raw) : raw;
    }

    /// <summary>
    /// Auto-detect HTML only when the payload looks like a real document.
    /// Avoid treating OCR/paste lines that merely contain '&lt;' / '&gt;' as HTML.
    /// </summary>
    internal static bool LooksLikeHtml(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return false;
        }

        var trimmed = content.AsSpan().TrimStart();
        if (trimmed.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<html", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<head", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("<body", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (content.Contains("application/ld+json", StringComparison.OrdinalIgnoreCase)
            && content.Contains("<script", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return content.Contains("<html", StringComparison.OrdinalIgnoreCase)
            && content.Contains("</html>", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<ImportRecipeResult> BuildResultAsync(
        string plainText,
        ImportRecipeParseOptions? parseOptions,
        CancellationToken ct)
    {
        var options = parseOptions ?? new ImportRecipeParseOptions();
        var aiEnabled = importOptions.Value.Ai.Enabled
            && !string.IsNullOrWhiteSpace(importOptions.Value.Ai.ApiKey);

        ImportRecipeResult parsed;
        if (options.UseFullRecipeAi && aiEnabled)
        {
            parsed = await recipeAiParser.ParseAsync(plainText, ct);
        }
        else
        {
            parsed = await recipeTextParser.ParseAsync(plainText, options, ct);
        }

        var ingredients = parsed.Ingredients.ToList();

        for (var i = 0; i < ingredients.Count; i++)
        {
            var line = ingredients[i];
            if (string.IsNullOrWhiteSpace(line.Name))
            {
                continue;
            }

            var match = await ingredientMatcher.MatchAsync(line.Name, ct);
            ingredients[i] = new ImportedIngredientLine
            {
                RawLine = line.RawLine,
                Quantity = line.Quantity,
                Unit = line.Unit,
                Name = line.Name,
                Preparation = line.Preparation,
                Confidence = line.Confidence,
                ParseMethod = line.ParseMethod,
                MatchType = match.MatchType,
            };
        }

        return new ImportRecipeResult
        {
            Title = parsed.Title,
            Description = parsed.Description,
            PreparationTimeMinutes = parsed.PreparationTimeMinutes,
            CookingTimeMinutes = parsed.CookingTimeMinutes,
            Difficulty = parsed.Difficulty,
            Category = parsed.Category,
            Servings = parsed.Servings,
            Source = parsed.Source,
            Ingredients = ingredients,
            Steps = parsed.Steps,
            Warnings = parsed.Warnings,
        };
    }
}
