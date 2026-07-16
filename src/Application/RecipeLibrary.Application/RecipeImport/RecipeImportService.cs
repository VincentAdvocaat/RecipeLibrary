using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;

namespace RecipeLibrary.Application.RecipeImport;

/// <summary>
/// Import entry: normalize any modality to plain text, then parse with <see cref="RecipeTextParser"/>.
/// </summary>
public sealed class RecipeImportService(
    RecipeTextParser recipeTextParser,
    HtmlRecipeTextExtractor htmlRecipeTextExtractor,
    IngredientMatcher ingredientMatcher)
{
    public async Task<ImportRecipeResult> ImportContentAsync(
        ImportRecipeContentQuery query,
        CancellationToken ct = default)
    {
        var text = ResolvePlainText(query.Content, query.ContentKind);
        return await BuildResultAsync(text, ct);
    }

    public async Task<ImportRecipeResult> ImportPlainTextAsync(string plainText, CancellationToken ct = default) =>
        await BuildResultAsync(plainText ?? string.Empty, ct);

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

    private static bool LooksLikeHtml(string content) =>
        content.Contains('<', StringComparison.Ordinal) && content.Contains('>', StringComparison.Ordinal);

    private async Task<ImportRecipeResult> BuildResultAsync(string plainText, CancellationToken ct)
    {
        var parsed = recipeTextParser.Parse(plainText);
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
            Source = ImportSource.PlainText,
            Ingredients = ingredients,
            Steps = parsed.Steps,
            Warnings = parsed.Warnings,
        };
    }
}
