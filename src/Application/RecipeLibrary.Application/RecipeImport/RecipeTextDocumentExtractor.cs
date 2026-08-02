using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.RecipeImport;

/// <summary>
/// Extracts recipe document sections from normalized plain text (clean-data format and noisy scrapes).
/// </summary>
public static class RecipeTextDocumentExtractor
{
    public static RecipeTextDocument Extract(string plainText)
    {
        var warnings = new List<string>();
        var rawLines = plainText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Select(static line => line.TrimEnd()).ToList();
        if (rawLines.All(string.IsNullOrWhiteSpace))
        {
            warnings.Add(ImportWarningCodes.NoContent);
            return new RecipeTextDocument { Warnings = warnings };
        }

        string? title = null;
        string? description = null;
        int? preparationMinutes = null;
        int? cookingMinutes = null;
        int? difficulty = null;
        int? servings = null;
        var ingredientLines = new List<string>();
        var instructionLines = new List<string>();
        var introBuffer = new List<string>();
        var section = Section.Preamble;
        var inIntroLabeled = false;
        var seenRecipeMeta = false;
        var ingredientsClosed = false;
        var inGarnishIngredients = false;

        for (var i = 0; i < rawLines.Count; i++)
        {
            var line = rawLines[i].Trim();
            if (line.Length == 0 || section == Section.Done) continue;

            if (TryGetLabeledHeader(line, RecipeTextSectionLexicon.IntroHeaders, out var introRest))
            {
                section = Section.Intro;
                inIntroLabeled = true;
                if (!string.IsNullOrWhiteSpace(introRest)) introBuffer.Add(introRest);
                continue;
            }
            if (TryGetLabeledHeader(line, RecipeTextSectionLexicon.IngredientSectionHeaders, out var ingredientTitle))
            {
                section = Section.Ingredients;
                ingredientsClosed = false;
                inGarnishIngredients = false;
                title ??= FindTitleAbove(rawLines, i);
                if (!string.IsNullOrWhiteSpace(ingredientTitle))
                {
                    if (RecipeMetaLineParser.TryParseServingsPhrase(ingredientTitle, out var headerServings)) servings = headerServings;
                    else title = ingredientTitle.Trim();
                }
                continue;
            }
            if (TryGetLabeledHeader(line, RecipeTextSectionLexicon.InstructionSectionHeaders, out var instructionTitle))
            {
                section = Section.Instructions;
                inGarnishIngredients = false;
                title ??= FindTitleAbove(rawLines, i);
                if (!string.IsNullOrWhiteSpace(instructionTitle)) title ??= instructionTitle.Trim();
                continue;
            }
            if (IsFooterSectionHeader(line))
            {
                if (section is Section.Ingredients or Section.Instructions) section = Section.Done;
                continue;
            }
            if (section is Section.Preamble or Section.Intro)
            {
                if (RecipeMetaLineParser.TryParseLabeledTime(line, out var labeledKind, out var labeledMinutes))
                {
                    if (labeledKind == RecipeTimeKind.Preparation) preparationMinutes = labeledMinutes;
                    else cookingMinutes = labeledMinutes;
                    seenRecipeMeta = true;
                    continue;
                }
                if (RecipeMetaLineParser.TryParseTime(line, out var minutes))
                {
                    cookingMinutes = minutes;
                    seenRecipeMeta = true;
                    continue;
                }
                if (RecipeMetaLineParser.TryParseDifficulty(line, out var diff))
                {
                    difficulty = diff;
                    seenRecipeMeta = true;
                    continue;
                }
                if (RecipeMetaLineParser.TryParseServingsPhrase(line, out var parsedServings))
                {
                    servings = parsedServings;
                    seenRecipeMeta = true;
                    continue;
                }
            }

            switch (section)
            {
                case Section.Intro:
                    if (!RecipeLineHeuristics.IsChromeLine(line)) introBuffer.Add(line);
                    break;
                case Section.Ingredients:
                    if (ingredientsClosed) break;
                    if (RecipeLineHeuristics.IsIngredientSubsectionHeader(line))
                    {
                        inGarnishIngredients = RecipeLineHeuristics.IsGarnishSubsectionHeader(line);
                        break;
                    }
                    if (RecipeLineHeuristics.IsChromeLine(line)) break;
                    if (RecipeLineHeuristics.IsLikelyIngredientLine(line))
                    {
                        var ingredient = RecipeLineHeuristics.StripBullet(line);
                        if (inGarnishIngredients && RecipeLineHeuristics.ShouldAnnotateAsGarnish(ingredient)) ingredient = $"{ingredient}, for garnish";
                        ingredientLines.Add(ingredient);
                    }
                    else if (ingredientLines.Count > 0) ingredientsClosed = true;
                    break;
                case Section.Instructions:
                    if (!RecipeLineHeuristics.IsStepHeadingOnly(line) && RecipeLineHeuristics.IsLikelyInstructionLine(line)) instructionLines.Add(RecipeLineHeuristics.StripNumberPrefix(line));
                    break;
                default:
                    if (!inIntroLabeled && !seenRecipeMeta && !RecipeLineHeuristics.IsChromeLine(line)) introBuffer.Add(line);
                    break;
            }
        }

        if (introBuffer.Count > 0)
        {
            if (title is null)
            {
                var titleIndex = FindPreambleTitleIndex(introBuffer);
                if (titleIndex >= 0)
                {
                    title = RecipeMetaLineParser.NormalizeTitleCandidate(introBuffer[titleIndex]);
                    introBuffer.RemoveAt(titleIndex);
                }
            }
            description = inIntroLabeled ? string.Join(" ", introBuffer).Trim() : SelectBestDescription(introBuffer);
        }
        if (ingredientLines.Count == 0)
        {
            warnings.Add(ImportWarningCodes.HeuristicIngredients);
            ingredientLines.AddRange(rawLines.Select(static line => line.Trim()).Where(static line => line.Length > 0).Where(RecipeLineHeuristics.IsLikelyIngredientLine).Select(RecipeLineHeuristics.StripBullet));
        }

        return new RecipeTextDocument
        {
            Title = title,
            Description = description,
            PreparationTimeMinutes = preparationMinutes,
            CookingTimeMinutes = cookingMinutes,
            Difficulty = difficulty,
            Servings = servings,
            IngredientLines = ingredientLines,
            Steps = instructionLines.Where(static line => !string.IsNullOrWhiteSpace(line)).Select((text, index) => new ImportedInstructionStep { StepNumber = index + 1, Text = text }).ToList(),
            Warnings = warnings,
        };
    }

    /// <summary>Rebuilds a chrome-stripped plain-text recipe for full-recipe AI parsing.</summary>
    public static string NormalizePlainTextForAi(string plainText) => RecipeTextDocumentFormatter.NormalizePlainTextForAi(plainText);

    public static string FormatNormalizedPlainText(RecipeTextDocument document) => RecipeTextDocumentFormatter.FormatNormalizedPlainText(document);

    /// <summary>Returns a canonical difficulty label (e.g. Makkelijk) when the line expresses difficulty.</summary>
    public static bool TryParseDifficultyLabel(string line, out string label) => RecipeMetaLineParser.TryParseDifficultyLabel(line, out label);

    private static int FindPreambleTitleIndex(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (RecipeLineHeuristics.IsTitleCandidate(lines[i])) return i;
        }
        return -1;
    }

    private static string? FindTitleAbove(IReadOnlyList<string> rawLines, int headerIndex)
    {
        for (var i = headerIndex - 1; i >= 0; i--)
        {
            var candidate = rawLines[i].Trim();
            if (candidate.Length == 0 || RecipeLineHeuristics.IsChromeLine(candidate)) continue;
            if (RecipeLineHeuristics.IsTitleCandidate(candidate)) return RecipeMetaLineParser.NormalizeTitleCandidate(candidate);
        }
        return null;
    }

    private static bool TryGetLabeledHeader(string line, IEnumerable<string> headers, out string remainder)
    {
        remainder = string.Empty;
        foreach (var header in headers)
        {
            if (line.Equals(header, StringComparison.OrdinalIgnoreCase)) return true;
            var prefix = header + ":";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                remainder = line[prefix.Length..].Trim();
                return true;
            }
        }
        return false;
    }

    private static bool IsFooterSectionHeader(string line)
    {
        var normalized = line.Trim().TrimEnd(':').Trim();
        return RecipeTextSectionLexicon.FooterSectionHeaders.Any(header =>
            normalized.Equals(header, StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith(header + " ", StringComparison.OrdinalIgnoreCase) && normalized.Length <= header.Length + 24);
    }

    private static string? SelectBestDescription(IReadOnlyList<string> lines) =>
        lines.Select(static line => line.Trim())
            .Where(static line => line.Length >= 80 && line.Contains('.', StringComparison.Ordinal))
            .Where(line => !RecipeLineHeuristics.IsChromeLine(line))
            .OrderByDescending(static line => line.Length)
            .FirstOrDefault();

    private enum Section { Preamble, Intro, Ingredients, Instructions, Done }
}

public sealed class RecipeTextDocument
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public int? PreparationTimeMinutes { get; init; }
    public int? CookingTimeMinutes { get; init; }
    public int? Difficulty { get; init; }
    public int? Category { get; init; }
    public int? Servings { get; init; }
    public IReadOnlyList<string> IngredientLines { get; init; } = [];
    public IReadOnlyList<ImportedInstructionStep> Steps { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
