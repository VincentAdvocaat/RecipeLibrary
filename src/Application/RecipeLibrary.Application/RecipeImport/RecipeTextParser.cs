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
    IOptions<RecipeImportOptions> importOptions)
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
            foreach (var (index, rawLine) in aiCandidates)
            {
                try
                {
                    var aiLines = await ingredientLineAiParser.ParseLinesAsync([rawLine], ct);
                    if (aiLines.Count == 0 || string.IsNullOrWhiteSpace(aiLines[0].Name))
                    {
                        continue;
                    }

                    var aiLine = aiLines[0];
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

                    if (!warnings.Contains(ImportWarningCodes.LowConfidenceAiHint))
                    {
                        warnings.Add(ImportWarningCodes.LowConfidenceAiHint);
                    }
                }
                catch (Exception)
                {
                    if (!warnings.Contains(ImportWarningCodes.LowConfidenceAiHint))
                    {
                        warnings.Add(ImportWarningCodes.LowConfidenceAiHint);
                    }
                }
            }
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
