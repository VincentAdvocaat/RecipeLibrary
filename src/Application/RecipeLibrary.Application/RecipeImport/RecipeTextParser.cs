using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;

namespace RecipeLibrary.Application.RecipeImport;

/// <summary>
/// Parses normalized recipe plain text into an import draft (shared by paste/URL/image).
/// Low-confidence ingredient lines can be upgraded via <see cref="IIngredientLineAiParser"/>.
/// </summary>
public sealed class RecipeTextParser(
    IngredientLineParser ingredientLineParser,
    IIngredientLineAiParser ingredientLineAiParser,
    IOptions<RecipeImportOptions> importOptions,
    ILogger<RecipeTextParser> logger)
{
    public Task<ImportRecipeResult> ParseAsync(
        string plainText,
        ImportRecipeParseOptions? parseOptions = null,
        CancellationToken ct = default)
    {
        var options = parseOptions ?? new ImportRecipeParseOptions();
        var document = RecipeTextDocumentExtractor.Extract(plainText ?? string.Empty);
        return BuildResultAsync(document, options, ct);
    }

    private async Task<ImportRecipeResult> BuildResultAsync(
        RecipeTextDocument document,
        ImportRecipeParseOptions parseOptions,
        CancellationToken ct)
    {
        var warnings = document.Warnings.ToList();
        var threshold = importOptions.Value.Ai.ConfidenceThreshold;
        var aiEnabled = importOptions.Value.Ai.Enabled
            && !string.IsNullOrWhiteSpace(importOptions.Value.Ai.ApiKey);
        var useAiFallback = parseOptions.UseAiFallback && aiEnabled;

        var ingredients = new List<ImportedIngredientLine>();
        var aiCandidates = new List<(int Index, string RawLine)>();

        foreach (var rawLine in document.IngredientLines)
        {
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var parsed = ingredientLineParser.Parse(rawLine);
            var index = ingredients.Count;
            ingredients.Add(ToImportedLine(parsed));

            if (useAiFallback && parsed.Confidence < threshold)
            {
                aiCandidates.Add((index, rawLine));
            }
        }

        if (aiCandidates.Count > 0)
        {
            await ApplyAiFallbackAsync(ingredients, aiCandidates, warnings, ct);
        }

        return new ImportRecipeResult
        {
            Title = document.Title,
            Description = document.Description,
            PreparationTimeMinutes = document.PreparationTimeMinutes,
            CookingTimeMinutes = document.CookingTimeMinutes,
            Difficulty = document.Difficulty,
            Category = document.Category,
            Servings = document.Servings,
            Source = ImportSource.PlainText,
            Ingredients = ingredients,
            Steps = document.Steps,
            Warnings = warnings,
        };
    }

    private async Task ApplyAiFallbackAsync(
        List<ImportedIngredientLine> ingredients,
        List<(int Index, string RawLine)> aiCandidates,
        List<string> warnings,
        CancellationToken ct)
    {
        try
        {
            var rawLines = aiCandidates.Select(static c => c.RawLine).ToList();
            var aiLines = await ingredientLineAiParser.ParseLinesAsync(rawLines, ct);
            var applied = false;

            for (var i = 0; i < aiCandidates.Count; i++)
            {
                if (i >= aiLines.Count || string.IsNullOrWhiteSpace(aiLines[i].Name))
                {
                    continue;
                }

                var (index, rawLine) = aiCandidates[i];
                var aiLine = aiLines[i];
                ingredients[index] = new ImportedIngredientLine
                {
                    RawLine = rawLine,
                    Quantity = aiLine.Quantity,
                    Unit = string.IsNullOrWhiteSpace(aiLine.Unit) ? null : aiLine.Unit,
                    Name = aiLine.Name,
                    Preparation = aiLine.Preparation,
                    Confidence = aiLine.Confidence > 0 ? aiLine.Confidence : 0.85m,
                    ParseMethod = ImportParseMethod.Ai,
                };
                applied = true;
            }

            if (applied)
            {
                AddWarning(warnings, ImportWarningCodes.LowConfidenceAiHint);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "AI ingredient-line fallback failed for {Count} low-confidence line(s).",
                aiCandidates.Count);
            AddWarning(warnings, ImportWarningCodes.AiFallbackFailed);
        }
    }

    private static void AddWarning(List<string> warnings, string code)
    {
        if (!warnings.Contains(code))
        {
            warnings.Add(code);
        }
    }

    private static ImportedIngredientLine ToImportedLine(ParsedIngredientLine parsed) =>
        new()
        {
            RawLine = parsed.RawLine,
            Quantity = parsed.Quantity,
            Unit = parsed.Unit,
            Name = parsed.Name,
            Preparation = parsed.Preparation,
            Confidence = parsed.Confidence,
            ParseMethod = parsed.ParseMethod,
        };
}
