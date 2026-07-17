using System.Globalization;
using System.Text;
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
        "steps",
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
        "serving suggestions",
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
        "youtube link",
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

    private static readonly Regex ServesPhrasePattern = new(
        @"^serves?\s+(?<count>\d+)(?:\s*(?:to|-|–)\s*\d+)?$",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>Matches Instagram-style "RECIPE (serves 2):" / "(serves 4)" lines.</summary>
    private static readonly Regex ServesInParenthesesPattern = new(
        @"\(\s*serves?\s+(?<count>\d+)(?:\s*(?:to|-|–)\s*\d+)?\s*\)",
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
        var inGarnishIngredients = false;

        for (var i = 0; i < rawLines.Count; i++)
        {
            var line = rawLines[i].Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (section == Section.Done)
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
                inGarnishIngredients = false;
                title ??= FindTitleAbove(rawLines, i);
                if (!string.IsNullOrWhiteSpace(ingredientTitle))
                {
                    if (TryParseServingsPhrase(ingredientTitle, out var headerServings))
                    {
                        servings = headerServings;
                    }
                    else
                    {
                        // Section titles are more specific than preamble headings.
                        title = ingredientTitle.Trim();
                    }
                }

                continue;
            }

            if (TryGetLabeledHeader(line, InstructionSectionHeaders, out var instructionTitle))
            {
                section = Section.Instructions;
                inGarnishIngredients = false;
                title ??= FindTitleAbove(rawLines, i);
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

                    if (IsIngredientSubsectionHeader(line))
                    {
                        inGarnishIngredients = IsGarnishSubsectionHeader(line);
                        break;
                    }

                    if (IsChromeLine(line))
                    {
                        break;
                    }

                    if (IsLikelyIngredientLine(line))
                    {
                        var ingredient = StripBullet(line);
                        if (inGarnishIngredients
                            && ShouldAnnotateAsGarnish(ingredient))
                        {
                            ingredient = $"{ingredient}, for garnish";
                        }

                        ingredientLines.Add(ingredient);
                    }
                    else if (ingredientLines.Count > 0)
                    {
                        ingredientsClosed = true;
                    }

                    break;
                case Section.Instructions:
                    if (IsStepHeadingOnly(line))
                    {
                        break;
                    }

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
                var titleIndex = FindPreambleTitleIndex(introBuffer);
                if (titleIndex >= 0)
                {
                    title = NormalizeTitleCandidate(introBuffer[titleIndex]);
                    introBuffer.RemoveAt(titleIndex);
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
    /// Rebuilds a chrome-stripped plain-text recipe for full-recipe AI parsing.
    /// Falls back to the original text when extraction yields no usable sections.
    /// </summary>
    public static string NormalizePlainTextForAi(string plainText)
    {
        var document = Extract(plainText ?? string.Empty);
        var normalized = FormatNormalizedPlainText(document);
        return string.IsNullOrWhiteSpace(normalized) ? plainText ?? string.Empty : normalized;
    }

    public static string FormatNormalizedPlainText(RecipeTextDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var sb = new StringBuilder();

        if (!string.IsNullOrWhiteSpace(document.Title))
        {
            sb.AppendLine(document.Title.Trim());
        }

        if (!string.IsNullOrWhiteSpace(document.Description))
        {
            EnsureBlankLine(sb);
            sb.AppendLine(document.Description.Trim());
        }

        var hasMeta = document.PreparationTimeMinutes is not null
            || document.CookingTimeMinutes is not null
            || document.Servings is not null;
        if (hasMeta)
        {
            EnsureBlankLine(sb);
            if (document.PreparationTimeMinutes is int prep)
            {
                sb.AppendLine($"Prep time: {prep} min");
            }

            if (document.CookingTimeMinutes is int cook)
            {
                sb.AppendLine($"Cook time: {cook} min");
            }

            if (document.Servings is int servings)
            {
                sb.AppendLine($"Servings: {servings}");
            }
        }

        if (document.IngredientLines.Count > 0)
        {
            EnsureBlankLine(sb);
            sb.AppendLine("Ingredients");
            foreach (var line in document.IngredientLines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    sb.AppendLine(line.Trim());
                }
            }
        }

        if (document.Steps.Count > 0)
        {
            EnsureBlankLine(sb);
            sb.AppendLine("Instructions");
            foreach (var step in document.Steps)
            {
                if (string.IsNullOrWhiteSpace(step.Text))
                {
                    continue;
                }

                var number = step.StepNumber > 0 ? step.StepNumber : 0;
                sb.AppendLine(number > 0 ? $"{number}. {step.Text.Trim()}" : step.Text.Trim());
            }
        }

        return sb.ToString().Trim();
    }

    private static void EnsureBlankLine(StringBuilder sb)
    {
        if (sb.Length == 0)
        {
            return;
        }

        sb.AppendLine();
    }

    private static int FindPreambleTitleIndex(IReadOnlyList<string> lines)
    {
        for (var i = 0; i < lines.Count; i++)
        {
            if (IsTitleCandidate(lines[i]))
            {
                return i;
            }
        }

        return -1;
    }

    private static string? FindTitleAbove(IReadOnlyList<string> rawLines, int headerIndex)
    {
        for (var i = headerIndex - 1; i >= 0; i--)
        {
            var candidate = rawLines[i].Trim();
            if (candidate.Length == 0 || IsChromeLine(candidate))
            {
                continue;
            }

            if (IsTitleCandidate(candidate))
            {
                return NormalizeTitleCandidate(candidate);
            }
        }

        return null;
    }

    private static bool IsTitleCandidate(string line)
    {
        var candidate = line.Trim();
        if (candidate.Length is < 6 or > 80
            || candidate.Contains('.', StringComparison.Ordinal)
            || IsChromeLine(candidate)
            || TryParseServingsPhrase(candidate, out _)
            || TryParseDifficulty(candidate, out _)
            || TryParseTime(candidate, out _))
        {
            return false;
        }

        var words = candidate
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length >= 2 && Regex.IsMatch(candidate, @"[A-Za-z]");
    }

    private static string NormalizeTitleCandidate(string line)
    {
        var trimmed = line.Trim();
        if (trimmed.Any(char.IsLetter) && !trimmed.Any(char.IsLower))
        {
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant());
        }

        return trimmed;
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

    private static bool TryParseServings(string line, out int servings) =>
        TryParseServingsPhrase(line, out servings);

    private static bool TryParseServingsPhrase(string line, out int servings)
    {
        servings = 0;
        var trimmed = line.Trim().TrimEnd(':').Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var serves = ServesPhrasePattern.Match(trimmed);
        if (serves.Success)
        {
            servings = int.Parse(serves.Groups["count"].Value, CultureInfo.InvariantCulture);
            return servings > 0 && servings <= 100;
        }

        var parenthetical = ServesInParenthesesPattern.Match(trimmed);
        if (parenthetical.Success)
        {
            servings = int.Parse(parenthetical.Groups["count"].Value, CultureInfo.InvariantCulture);
            return servings > 0 && servings <= 100;
        }

        var match = ServingsPattern.Match(trimmed);
        if (!match.Success)
        {
            return false;
        }

        servings = int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture);
        return servings > 0 && servings <= 100;
    }

    private static bool IsIngredientSubsectionHeader(string line)
    {
        var trimmed = StripBullet(line);
        if (trimmed.Length == 0 || LooksLikeMeasuredOrToTasteIngredient(trimmed))
        {
            return false;
        }

        if (trimmed.StartsWith("For the ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("For ", StringComparison.OrdinalIgnoreCase)
                && trimmed.EndsWith(':'))
        {
            return true;
        }

        var withoutColon = trimmed.TrimEnd(':').Trim();
        return withoutColon.Equals("Other Ingredients", StringComparison.OrdinalIgnoreCase)
            || withoutColon.Equals("Additional Ingredients", StringComparison.OrdinalIgnoreCase)
            || (trimmed.EndsWith(':') && !Regex.IsMatch(trimmed, @"\d"));
    }

    private static bool IsGarnishSubsectionHeader(string line)
    {
        var normalized = StripBullet(line).TrimEnd(':').Trim();
        return normalized.Contains("garnish", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("finishing", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldAnnotateAsGarnish(string ingredientLine)
    {
        if (ingredientLine.Contains("for garnish", StringComparison.OrdinalIgnoreCase)
            || ingredientLine.Contains("for drizzling", StringComparison.OrdinalIgnoreCase)
            || ingredientLine.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Alternative ingredient notes stay as-is (e.g. "paprika (or Kashmiri chili powder)").
        if (ingredientLine.Contains("(or ", StringComparison.OrdinalIgnoreCase)
            || ingredientLine.Contains(", or ", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsStepHeadingOnly(string line)
    {
        var trimmed = StripBullet(line);
        var match = Regex.Match(
            trimmed,
            @"^Step\s+\d+\s*:\s*(?<title>.*)$",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return false;
        }

        var title = match.Groups["title"].Value.Trim();
        // Dedicated step captions (body follows on next lines).
        return title.Length is > 0 and < 80
            && !title.Contains('.', StringComparison.Ordinal);
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
            // Allow glued units common in social captions ("14oz", "500g", "2tbsp").
            @"^(?:[½¼¾⅓⅔]|\d+(?:[.,]\d+)?|\d+\s*/\s*\d+|\d+\s*-\s*\d+|\d+\s+to\s+\d+|snuf|snufje|snufjes|handje|handjes|beetje|teen|teentje)(?:[a-zA-Z]{1,4})?(?:\b|\s|$)",
            RegexOptions.IgnoreCase)
        || Regex.IsMatch(trimmed, @"^juice\s+of\s+\d+\b", RegexOptions.IgnoreCase)
        || trimmed.Contains(" naar smaak", StringComparison.OrdinalIgnoreCase)
        || trimmed.Contains(" to taste", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeBareIngredientName(string trimmed)
    {
        if (trimmed.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed["Additional ".Length..].Trim();
        }

        if (trimmed.Length is 0 or > 90)
        {
            return false;
        }

        if (trimmed.Contains('?', StringComparison.Ordinal))
        {
            return false;
        }

        // Allow a single trailing period-less phrase; reject multi-sentence chrome.
        if (trimmed.Contains(". ", StringComparison.Ordinal))
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
        return words.Length is >= 1 and <= 12;
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

        if (IsHashtagOnlyLine(normalized))
        {
            return true;
        }

        if (Regex.IsMatch(normalized, @"^(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)$", RegexOptions.IgnoreCase)
            || Regex.IsMatch(normalized, @"^\d{1,2}$"))
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

    /// <summary>Instagram/TikTok trailing tag rows like "#shrimpbowl #proteinbowl".</summary>
    private static bool IsHashtagOnlyLine(string line)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return tokens.Length > 0 && tokens.All(static t => t.StartsWith('#') && t.Length > 1);
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
