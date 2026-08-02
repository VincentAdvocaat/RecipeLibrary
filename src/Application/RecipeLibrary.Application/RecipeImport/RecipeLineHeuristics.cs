using System.Text.RegularExpressions;

namespace RecipeLibrary.Application.RecipeImport;

internal static class RecipeLineHeuristics
{
    internal static bool IsIngredientSubsectionHeader(string line)
    {
        var trimmed = StripBullet(line);
        if (trimmed.Length == 0 || LooksLikeMeasuredOrToTasteIngredient(trimmed)) return false;
        if (trimmed.StartsWith("For the ", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("For ", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(':'))
        {
            return true;
        }

        var withoutColon = trimmed.TrimEnd(':').Trim();
        return withoutColon.Equals("Other Ingredients", StringComparison.OrdinalIgnoreCase)
            || withoutColon.Equals("Additional Ingredients", StringComparison.OrdinalIgnoreCase)
            || trimmed.EndsWith(':') && !Regex.IsMatch(trimmed, @"\d");
    }

    internal static bool IsGarnishSubsectionHeader(string line) =>
        StripBullet(line).TrimEnd(':').Trim().Contains("garnish", StringComparison.OrdinalIgnoreCase)
        || StripBullet(line).TrimEnd(':').Trim().Contains("finishing", StringComparison.OrdinalIgnoreCase);

    internal static bool ShouldAnnotateAsGarnish(string ingredientLine) =>
        !ingredientLine.Contains("for garnish", StringComparison.OrdinalIgnoreCase)
        && !ingredientLine.Contains("for drizzling", StringComparison.OrdinalIgnoreCase)
        && !ingredientLine.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase)
        && !ingredientLine.Contains("(or ", StringComparison.OrdinalIgnoreCase)
        && !ingredientLine.Contains(", or ", StringComparison.OrdinalIgnoreCase);

    internal static bool IsStepHeadingOnly(string line)
    {
        var match = Regex.Match(StripBullet(line), @"^Step\s+\d+\s*:\s*(?<title>.*)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        var title = match.Groups["title"].Value.Trim();
        return match.Success && title.Length is > 0 and < 80 && !title.Contains('.', StringComparison.Ordinal);
    }

    internal static bool IsLikelyIngredientLine(string line)
    {
        if (IsChromeLine(line) || RecipeTextSectionLexicon.MediaFilePattern.IsMatch(line) || line.Contains('€')) return false;
        var trimmed = StripBullet(line);
        return LooksLikeMeasuredOrToTasteIngredient(trimmed) || LooksLikeBareIngredientName(trimmed);
    }

    internal static bool IsLikelyInstructionLine(string line)
    {
        if (IsChromeLine(line) || RecipeTextSectionLexicon.MediaFilePattern.IsMatch(line) || line.Contains('€')) return false;
        var trimmed = StripNumberPrefix(line);
        if (trimmed.Length < 8) return false;
        var stepHits = RecipeTextSectionLexicon.StepCaptionNoisePattern.Matches(trimmed);
        return stepHits.Count < 2 && (stepHits.Count != 1 || trimmed.Length >= 40) && trimmed.Contains(' ', StringComparison.Ordinal);
    }

    internal static bool IsChromeLine(string line)
    {
        var normalized = line.Trim();
        if (normalized.Length == 0) return false;
        if (RecipeTextSectionLexicon.ChromeExactLines.Contains(normalized) || IsHashtagOnlyLine(normalized)) return true;
        if (Regex.IsMatch(normalized, @"^(?:jan|feb|mar|apr|may|jun|jul|aug|sep|sept|oct|nov|dec)$", RegexOptions.IgnoreCase) || Regex.IsMatch(normalized, @"^\d{1,2}$")) return true;
        var lower = normalized.ToLowerInvariant();
        return RecipeTextSectionLexicon.ChromeLinePrefixes.Any(prefix => lower.StartsWith(prefix, StringComparison.Ordinal));
    }

    internal static string StripBullet(string line) => line.TrimStart('-', '•', '*', '▢', ' ').Trim();
    internal static string StripNumberPrefix(string line) => Regex.Replace(line, @"^\d+[\.)]\s*", string.Empty).Trim();

    internal static bool IsTitleCandidate(string line)
    {
        var candidate = line.Trim();
        if (candidate.Length is < 6 or > 80 || candidate.Contains('.', StringComparison.Ordinal) || IsChromeLine(candidate) || RecipeMetaLineParser.TryParseServingsPhrase(candidate, out _) || RecipeMetaLineParser.TryParseDifficulty(candidate, out _) || RecipeMetaLineParser.TryParseTime(candidate, out _)) return false;
        return candidate.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length >= 2 && Regex.IsMatch(candidate, @"[A-Za-z]");
    }

    private static bool LooksLikeMeasuredOrToTasteIngredient(string trimmed) =>
        Regex.IsMatch(trimmed, @"^(?:[½¼¾⅓⅔]|\d+(?:[.,]\d+)?|\d+\s*/\s*\d+|\d+\s*-\s*\d+|\d+\s+to\s+\d+|snuf|snufje|snufjes|handje|handjes|beetje|teen|teentje)(?:[a-zA-Z]{1,4})?(?:\b|\s|$)", RegexOptions.IgnoreCase)
        || Regex.IsMatch(trimmed, @"^juice\s+of\s+\d+\b", RegexOptions.IgnoreCase)
        || trimmed.Contains(" naar smaak", StringComparison.OrdinalIgnoreCase)
        || trimmed.Contains(" to taste", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeBareIngredientName(string trimmed)
    {
        if (trimmed.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase)) trimmed = trimmed["Additional ".Length..].Trim();
        if (trimmed.Length is 0 or > 90 || trimmed.Contains('?') || trimmed.Contains(". ", StringComparison.Ordinal) || trimmed.Contains("http", StringComparison.OrdinalIgnoreCase)) return false;
        if (!trimmed.Contains(' ') && (trimmed.Contains('-') || trimmed.Length > 18) || Regex.IsMatch(trimmed, @"\d")) return false;
        return trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length is >= 1 and <= 12;
    }

    private static bool IsHashtagOnlyLine(string line) =>
        line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).All(static token => token.StartsWith('#') && token.Length > 1);
}
