using System.Globalization;
using System.Text.RegularExpressions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.RecipeImport;

/// <summary>
/// Extracts recipe document sections from normalized plain text (clean-data format and noisy scrapes).
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
        "method",
        "directions",
    ];

    private static readonly string[] IntroHeaders =
    [
        "inleiding",
        "beschrijving",
        "omschrijving",
        "description",
    ];

    private static readonly string[] FooterSectionHeaders =
    [
        "tips",
        "tip",
        "beoordelingen",
        "reviews",
        "handig",
        "veelgestelde vragen",
        "faq",
        "gerelateerd",
        "related",
        "serveer met",
        "voedingswaarde",
        "nutrition",
        "opmerkingen",
        "comments",
        "notities",
        "notes",
    ];

    private static readonly string[] ChromeLinePrefixes =
    [
        "markeer als",
        "check off",
        "print recept",
        "print recipe",
        "kookstand",
        "cook mode",
        "recept opslaan",
        "save recipe",
        "of deel",
        "share via",
        "direct in je",
        "raak dit",
        "stuur dit",
        "bewaar in",
        "e-mailadres",
        "emailadres",
        "merknamen in",
        "affiliate",
        "zet de kookstand",
        "ga naar de inhoud",
        "ga naar boven",
        "naar voorbeeld",
        "naar mijn",
        "scroll naar",
        "laatst bijgewerkt",
        "gemaakt door",
        "opgeslagen",
        "aanmelden",
    ];

    private static readonly HashSet<string> ChromeExactLines = new(StringComparer.OrdinalIgnoreCase)
    {
        "whatsapp",
        "facebook",
        "pinterest",
        "instagram",
        "tiktok",
        "youtube",
        "home",
        "recepten",
        "zoeken",
        "e-mail",
        "email",
        "vegan recept",
        "vegetarisch recept",
        "lactose arm recept",
        "lactosevrij recept",
    };

    private static readonly Regex TimePattern = new(
        @"^(?:(?<minutes>\d+)\s*M(?:in(?:uten)?)?|(?<hours>\d+)\s*U(?:ur)?(?:\s*(?<minutes2>\d+)\s*M(?:in(?:uten)?)?)?)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LabeledTimePattern = new(
        @"^(?<label>bereidingstijd|prep(?:aration)?(?:\s*time)?|voorbereidingstijd|kooktijd|baktijd|cook(?:ing)?(?:\s*time)?)\s*:?\s*(?<rest>.+)$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ServingsPattern = new(
        @"^(?:voor\s+)?(?<count>\d+)\s*(?:personen|persoon|porties|portie|servings|serving|stuks|stuk)?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DifficultyLabeledPattern = new(
        @"(?:moeilijkheidsgraad|difficulty|niveau)[^.\n]{0,60}:\s*(?<level>makkelijk|gemiddeld|moeilijk|easy|medium|hard|normaal)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DifficultyTrailingPattern = new(
        @"\b(?<level>makkelijk|gemiddeld|moeilijk|easy|medium|hard|normaal)\s*\.?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex StepCaptionNoisePattern = new(
        @"(?:stap|step)\s*\d+",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MediaFilePattern = new(
        @"\.(?:png|jpe?g|webp|gif|svg)(?:\b|$)",
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

        for (var i = 0; i < rawLines.Count; i++)
        {
            var line = rawLines[i].Trim();
            if (line.Length == 0)
            {
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
                ingredientsClosed = false;
                if (!string.IsNullOrWhiteSpace(ingredientTitle))
                {
                    // Section titles are more specific than preamble headings.
                    title = ingredientTitle.Trim();
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

            if (IsFooterSectionHeader(line))
            {
                if (section is Section.Ingredients or Section.Instructions)
                {
                    section = Section.Done;
                }

                continue;
            }

            if (section is Section.Preamble or Section.Intro)
            {
                if (TryParseLabeledTime(line, out var labeledKind, out var labeledMinutes))
                {
                    if (labeledKind == TimeKind.Preparation)
                    {
                        preparationMinutes = labeledMinutes;
                    }
                    else
                    {
                        cookingMinutes = labeledMinutes;
                    }

                    seenRecipeMeta = true;
                    continue;
                }

                if (TryParseTime(line, out var minutes))
                {
                    // Unlabeled duration (e.g. "30 M") maps to cooking time for clean paste fixtures.
                    cookingMinutes = minutes;
                    seenRecipeMeta = true;
                    continue;
                }

                if (TryParseDifficulty(line, out var diff))
                {
                    difficulty = diff;
                    seenRecipeMeta = true;
                    continue;
                }

                if (TryParseServings(line, out var parsedServings))
                {
                    servings = parsedServings;
                    seenRecipeMeta = true;
                    continue;
                }
            }

            switch (section)
            {
                case Section.Intro:
                    if (!IsChromeLine(line))
                    {
                        introBuffer.Add(line);
                    }

                    break;
                case Section.Ingredients:
                    if (ingredientsClosed)
                    {
                        break;
                    }

                    if (IsLikelyIngredientLine(line))
                    {
                        ingredientLines.Add(StripBullet(line));
                    }
                    else if (IsChromeLine(line))
                    {
                        break;
                    }
                    else if (ingredientLines.Count > 0)
                    {
                        ingredientsClosed = true;
                    }

                    break;
                case Section.Instructions:
                    if (IsLikelyInstructionLine(line))
                    {
                        instructionLines.Add(StripNumberPrefix(line));
                    }

                    break;
                case Section.Done:
                    break;
                default:
                    // Unlabeled preamble: keep prose only until time/difficulty/servings meta appears.
                    if (!inIntroLabeled && !seenRecipeMeta && !IsChromeLine(line))
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

            description = inIntroLabeled
                ? string.Join(" ", introBuffer).Trim()
                : SelectBestDescription(introBuffer);
        }

        if (ingredientLines.Count == 0)
        {
            warnings.Add(ImportWarningCodes.HeuristicIngredients);
            ingredientLines.AddRange(
                rawLines
                    .Select(x => x.Trim())
                    .Where(x => x.Length > 0)
                    .Where(IsLikelyIngredientLine)
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
            PreparationTimeMinutes = preparationMinutes,
            CookingTimeMinutes = cookingMinutes,
            Difficulty = difficulty,
            Servings = servings,
            IngredientLines = ingredientLines,
            Steps = steps,
            Warnings = warnings,
        };
    }

    /// <summary>
    /// Returns a canonical difficulty label (e.g. Makkelijk) when the line expresses difficulty.
    /// </summary>
    public static bool TryParseDifficultyLabel(string line, out string label)
    {
        label = string.Empty;
        if (!TryParseDifficulty(line, out var difficulty))
        {
            return false;
        }

        label = difficulty switch
        {
            (int)Difficulty.Easy => "Makkelijk",
            (int)Difficulty.Medium => "Gemiddeld",
            (int)Difficulty.Hard => "Moeilijk",
            _ => string.Empty,
        };
        return label.Length > 0;
    }

    private enum Section
    {
        Preamble,
        Intro,
        Ingredients,
        Instructions,
        Done,
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

    private static bool IsFooterSectionHeader(string line)
    {
        var normalized = line.Trim().TrimEnd(':').Trim();
        foreach (var header in FooterSectionHeaders)
        {
            if (normalized.Equals(header, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (normalized.StartsWith(header + " ", StringComparison.OrdinalIgnoreCase)
                && normalized.Length <= header.Length + 24)
            {
                return true;
            }
        }

        return false;
    }

    private enum TimeKind
    {
        Preparation,
        Cooking,
    }

    private static bool TryParseLabeledTime(string line, out TimeKind kind, out int minutes)
    {
        kind = TimeKind.Cooking;
        minutes = 0;
        var labeled = LabeledTimePattern.Match(line.Trim());
        if (!labeled.Success)
        {
            return false;
        }

        var label = labeled.Groups["label"].Value.ToLowerInvariant();
        kind = label.StartsWith("bereid", StringComparison.Ordinal)
            || label.StartsWith("prep", StringComparison.Ordinal)
            || label.StartsWith("voorbereid", StringComparison.Ordinal)
            ? TimeKind.Preparation
            : TimeKind.Cooking;

        return TryParseTime(labeled.Groups["rest"].Value.Trim(), out minutes);
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
        var normalized = line.Trim();
        if (normalized.Length == 0)
        {
            return false;
        }

        if (TryMapDifficultyWord(normalized.ToLowerInvariant(), out difficulty))
        {
            return true;
        }

        var labeled = DifficultyLabeledPattern.Match(normalized);
        if (labeled.Success && TryMapDifficultyWord(labeled.Groups["level"].Value.ToLowerInvariant(), out difficulty))
        {
            return true;
        }

        if (normalized.Length <= 120)
        {
            var trailing = DifficultyTrailingPattern.Match(normalized);
            if (trailing.Success
                && TryMapDifficultyWord(trailing.Groups["level"].Value.ToLowerInvariant(), out difficulty)
                && (normalized.Contains(':', StringComparison.Ordinal) || normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryMapDifficultyWord(string word, out int difficulty)
    {
        difficulty = word switch
        {
            "makkelijk" or "easy" => (int)Difficulty.Easy,
            "gemiddeld" or "medium" or "normaal" => (int)Difficulty.Medium,
            "moeilijk" or "hard" => (int)Difficulty.Hard,
            _ => 0,
        };
        return difficulty != 0;
    }

    private static bool TryParseServings(string line, out int servings)
    {
        servings = 0;
        var match = ServingsPattern.Match(line.Trim());
        if (!match.Success)
        {
            return false;
        }

        servings = int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture);
        return servings > 0 && servings <= 100;
    }

    private static string? SelectBestDescription(IReadOnlyList<string> lines)
    {
        var prose = lines
            .Select(x => x.Trim())
            .Where(x => x.Length >= 80)
            .Where(x => x.Contains('.', StringComparison.Ordinal))
            .Where(x => !IsChromeLine(x))
            .OrderByDescending(x => x.Length)
            .FirstOrDefault();

        return string.IsNullOrWhiteSpace(prose) ? null : prose;
    }

    private static bool IsLikelyIngredientLine(string line)
    {
        if (IsChromeLine(line) || MediaFilePattern.IsMatch(line) || line.Contains('€'))
        {
            return false;
        }

        var trimmed = StripBullet(line);
        if (LooksLikeMeasuredOrToTasteIngredient(trimmed))
        {
            return true;
        }

        return LooksLikeBareIngredientName(trimmed);
    }

    private static bool LooksLikeMeasuredOrToTasteIngredient(string trimmed) =>
        Regex.IsMatch(
            trimmed,
            @"^(\d+(?:[.,]\d+)?|\d+\s*/\s*\d+|\d+\s*-\s*\d+|snuf|snufje|snufjes|handje|handjes|beetje|teen|teentje)\b",
            RegexOptions.IgnoreCase)
        || trimmed.Contains(" naar smaak", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeBareIngredientName(string trimmed)
    {
        if (trimmed.Length is 0 or > 60)
        {
            return false;
        }

        if (trimmed.Contains('.', StringComparison.Ordinal) || trimmed.Contains('?', StringComparison.Ordinal))
        {
            return false;
        }

        if (trimmed.Contains("http", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Brand/store slugs and mashed UI tokens (e.g. "albert-heijnjumbo").
        if (!trimmed.Contains(' ', StringComparison.Ordinal)
            && (trimmed.Contains('-', StringComparison.Ordinal) || trimmed.Length > 18))
        {
            return false;
        }

        // Measured lines without a leading quantity word are handled elsewhere; reject digits here
        // so UI chrome with numbers is not treated as a bare name.
        if (Regex.IsMatch(trimmed, @"\d"))
        {
            return false;
        }

        var words = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length is >= 1 and <= 6;
    }

    private static bool IsLikelyInstructionLine(string line)
    {
        if (IsChromeLine(line) || MediaFilePattern.IsMatch(line) || line.Contains('€'))
        {
            return false;
        }

        var trimmed = StripNumberPrefix(line);
        if (trimmed.Length < 8)
        {
            return false;
        }

        var stepHits = StepCaptionNoisePattern.Matches(trimmed);
        if (stepHits.Count >= 2)
        {
            return false;
        }

        if (stepHits.Count == 1 && trimmed.Length < 40)
        {
            return false;
        }

        return trimmed.Contains(' ', StringComparison.Ordinal);
    }

    private static bool IsChromeLine(string line)
    {
        var normalized = line.Trim();
        if (normalized.Length == 0)
        {
            return false;
        }

        if (ChromeExactLines.Contains(normalized))
        {
            return true;
        }

        var lower = normalized.ToLowerInvariant();
        foreach (var prefix in ChromeLinePrefixes)
        {
            // Start-of-line only: Contains() falsely drops real steps like "Ga naar de oven…".
            if (lower.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string StripBullet(string line) =>
        line.TrimStart('-', '•', '*', '▢', ' ').Trim();

    private static string StripNumberPrefix(string line) =>
        Regex.Replace(line, @"^\d+[\.)]\s*", string.Empty).Trim();
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
