using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.RecipeImport;

/// <summary>
/// Extracts recipe document sections from normalized plain text (clean-data format).
/// </summary>
public static class RecipeTextDocumentExtractor
{
    private static readonly string[] IngredientSectionHeaders =
    [
        "ingrediënten",
        "ingredienten",
        "benodigdheden",
        "ingredients",
    ];

    private static readonly string[] InstructionSectionHeaders =
    [
        "bereiding",
        "werkwijze",
        "instructies",
        "stappen",
        "instructions",
    ];

    private static readonly string[] IntroHeaders =
    [
        "inleiding",
        "beschrijving",
        "omschrijving",
        "description",
    ];

    private static readonly Regex TimePattern = new(
        @"^(?:(?<minutes>\d+)\s*M(?:in(?:uten)?)?|(?<hours>\d+)\s*U(?:ur)?(?:\s*(?<minutes2>\d+)\s*M(?:in(?:uten)?)?)?)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static RecipeTextDocument Extract(string plainText)
    {
        var warnings = new List<string>();
        var rawLines = plainText
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split('\n')
            .Select(x => x.TrimEnd())
            .ToList();

        if (rawLines.All(string.IsNullOrWhiteSpace))
        {
            warnings.Add(ImportWarningCodes.NoContent);
            return new RecipeTextDocument { Warnings = warnings };
        }

        string? title = null;
        string? description = null;
        int? cookingMinutes = null;
        int? difficulty = null;
        var ingredientLines = new List<string>();
        var instructionLines = new List<string>();
        var introBuffer = new List<string>();

        var section = Section.Preamble;
        var inIntroLabeled = false;

        for (var i = 0; i < rawLines.Count; i++)
        {
            var line = rawLines[i].Trim();
            if (line.Length == 0)
            {
                if (section == Section.Instructions && instructionLines.Count > 0)
                {
                    // Keep paragraph breaks as separate steps only when content follows; blank ignored.
                }

                continue;
            }

            if (TryGetLabeledHeader(line, IntroHeaders, out var introRest))
            {
                section = Section.Intro;
                inIntroLabeled = true;
                if (!string.IsNullOrWhiteSpace(introRest))
                {
                    introBuffer.Add(introRest);
                }

                continue;
            }

            if (TryGetLabeledHeader(line, IngredientSectionHeaders, out var ingredientTitle))
            {
                section = Section.Ingredients;
                if (!string.IsNullOrWhiteSpace(ingredientTitle))
                {
                    title ??= ingredientTitle.Trim();
                }

                continue;
            }

            if (TryGetLabeledHeader(line, InstructionSectionHeaders, out var instructionTitle))
            {
                section = Section.Instructions;
                if (!string.IsNullOrWhiteSpace(instructionTitle))
                {
                    title ??= instructionTitle.Trim();
                }

                continue;
            }

            if (section is Section.Preamble or Section.Intro)
            {
                if (TryParseTime(line, out var minutes))
                {
                    cookingMinutes = minutes;
                    continue;
                }

                if (TryParseDifficulty(line, out var diff))
                {
                    difficulty = diff;
                    continue;
                }
            }

            switch (section)
            {
                case Section.Intro:
                    introBuffer.Add(line);
                    break;
                case Section.Ingredients:
                    ingredientLines.Add(StripBullet(line));
                    break;
                case Section.Instructions:
                    instructionLines.Add(StripNumberPrefix(line));
                    break;
                default:
                    if (!inIntroLabeled)
                    {
                        introBuffer.Add(line);
                    }

                    break;
            }
        }

        if (introBuffer.Count > 0)
        {
            if (title is null)
            {
                var first = introBuffer[0].Trim();
                if (first.Length > 0 && first.Length <= 80 && !first.Contains('.', StringComparison.Ordinal))
                {
                    title = first;
                    introBuffer.RemoveAt(0);
                }
            }

            if (introBuffer.Count > 0)
            {
                description = string.Join(" ", introBuffer).Trim();
            }
        }

        if (ingredientLines.Count == 0)
        {
            warnings.Add(ImportWarningCodes.HeuristicIngredients);
            ingredientLines.AddRange(
                rawLines
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .Where(LooksLikeIngredientLine)
                    .Select(StripBullet));
        }

        var steps = instructionLines
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select((text, index) => new ImportedInstructionStep { StepNumber = index + 1, Text = text })
            .ToList();

        return new RecipeTextDocument
        {
            Title = title,
            Description = description,
            CookingTimeMinutes = cookingMinutes,
            Difficulty = difficulty,
            IngredientLines = ingredientLines,
            Steps = steps,
            Warnings = warnings,
        };
    }

    private enum Section
    {
        Preamble,
        Intro,
        Ingredients,
        Instructions,
    }

    private static bool TryGetLabeledHeader(string line, string[] headers, out string remainder)
    {
        remainder = string.Empty;
        foreach (var header in headers)
        {
            if (line.Equals(header, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var prefix = header + ":";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                remainder = line[prefix.Length..].Trim();
                return true;
            }
        }

        return false;
    }

    private static bool TryParseTime(string line, out int minutes)
    {
        minutes = 0;
        var match = TimePattern.Match(line.Trim());
        if (!match.Success)
        {
            return false;
        }

        if (match.Groups["minutes"].Success)
        {
            minutes = int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture);
            return minutes > 0;
        }

        var hours = int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
        var extra = match.Groups["minutes2"].Success
            ? int.Parse(match.Groups["minutes2"].Value, CultureInfo.InvariantCulture)
            : 0;
        minutes = hours * 60 + extra;
        return minutes > 0;
    }

    private static bool TryParseDifficulty(string line, out int difficulty)
    {
        difficulty = 0;
        var normalized = line.Trim().ToLowerInvariant();
        difficulty = normalized switch
        {
            "makkelijk" or "easy" => (int)Difficulty.Easy,
            "gemiddeld" or "medium" or "normaal" => (int)Difficulty.Medium,
            "moeilijk" or "hard" => (int)Difficulty.Hard,
            _ => 0,
        };
        return difficulty != 0;
    }

    private static string StripBullet(string line) =>
        line.TrimStart('-', '•', '*', '▢', ' ').Trim();

    private static string StripNumberPrefix(string line) =>
        Regex.Replace(line, @"^\d+[\.)]\s*", string.Empty).Trim();

    private static bool LooksLikeIngredientLine(string line)
    {
        var trimmed = StripBullet(line);
        return Regex.IsMatch(
                   trimmed,
                   @"^(\d+(?:[.,]\d+)?|\d+\s*/\s*\d+|\d+\s*-\s*\d+|snuf|snufje|snufjes|handje|handjes|beetje)\b",
                   RegexOptions.IgnoreCase)
               || trimmed.Contains(" naar smaak", StringComparison.OrdinalIgnoreCase);
    }
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
