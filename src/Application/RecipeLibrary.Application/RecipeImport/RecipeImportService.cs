using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;

namespace RecipeLibrary.Application.RecipeImport;

public sealed class RecipeImportService(
    StructuredRecipeExtractor structuredRecipeExtractor,
    IngredientLineParser ingredientLineParser,
    IngredientMatcher ingredientMatcher,
    IIngredientLineAiParser aiParser,
    IOptions<RecipeImportOptions> options)
{
    public async Task<ImportRecipeResult> ImportContentAsync(
        ImportRecipeContentQuery query,
        CancellationToken ct = default)
    {
        var extraction = structuredRecipeExtractor.Extract(query.Content, query.ContentKind);
        return await BuildResultAsync(extraction, ct);
    }

    private async Task<ImportRecipeResult> BuildResultAsync(StructuredRecipeExtraction extraction, CancellationToken ct)
    {
        var warnings = extraction.Warnings.ToList();
        var parsedLines = new List<ImportedIngredientLine>();
        var aiCandidates = new List<(int Index, string RawLine)>();

        for (var i = 0; i < extraction.IngredientLines.Count; i++)
        {
            var rawLine = extraction.IngredientLines[i];
            if (string.IsNullOrWhiteSpace(rawLine))
            {
                continue;
            }

            var parsed = ingredientLineParser.Parse(rawLine);
            var imported = MapParsedLine(parsed, ImportParseMethod.Deterministic);
            parsedLines.Add(imported);

            if (imported.Confidence < options.Value.Ai.ConfidenceThreshold)
            {
                aiCandidates.Add((parsedLines.Count - 1, rawLine));
            }
        }

        if (aiCandidates.Count > 0 && options.Value.Ai.Enabled)
        {
            var aiLines = await aiParser.ParseLinesAsync(
                aiCandidates.Select(x => x.RawLine).ToList(),
                ct);

            for (var i = 0; i < aiCandidates.Count && i < aiLines.Count; i++)
            {
                var (index, _) = aiCandidates[i];
                var aiLine = aiLines[i];
                parsedLines[index] = new ImportedIngredientLine
                {
                    RawLine = aiLine.RawLine,
                    Quantity = aiLine.Quantity,
                    Unit = aiLine.Unit,
                    Name = aiLine.Name,
                    Preparation = aiLine.Preparation,
                    Confidence = aiLine.Confidence,
                    ParseMethod = ImportParseMethod.Ai,
                };
            }
        }
        else if (aiCandidates.Count > 0 && !options.Value.Ai.Enabled)
        {
            warnings.Add(ImportWarningCodes.LowConfidenceAiHint);
        }

        for (var i = 0; i < parsedLines.Count; i++)
        {
            var line = parsedLines[i];
            if (string.IsNullOrWhiteSpace(line.Name))
            {
                continue;
            }

            var match = await ingredientMatcher.MatchAsync(line.Name, ct);
            parsedLines[i] = new ImportedIngredientLine
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
            Title = extraction.Title,
            Description = extraction.Description,
            PreparationTimeMinutes = extraction.PreparationTimeMinutes,
            CookingTimeMinutes = extraction.CookingTimeMinutes,
            Source = extraction.Source,
            Ingredients = parsedLines,
            Steps = extraction.Steps,
            Warnings = warnings,
        };
    }

    private static ImportedIngredientLine MapParsedLine(ParsedIngredientLine parsed, ImportParseMethod method) =>
        new()
        {
            RawLine = parsed.RawLine,
            Quantity = parsed.Quantity,
            Unit = parsed.Unit,
            Name = parsed.Name,
            Preparation = parsed.Preparation,
            Confidence = parsed.Confidence,
            ParseMethod = method,
        };
}
