using System.Globalization;
using System.Text.RegularExpressions;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.RecipeImport;

internal enum RecipeTimeKind { Preparation, Cooking }

internal static class RecipeMetaLineParser
{
    private static readonly Regex TimePattern = new(@"^(?:(?<minutes>\d+)\s*M(?:in(?:uten)?)?|(?<hours>\d+)\s*U(?:ur)?(?:\s*(?<minutes2>\d+)\s*M(?:in(?:uten)?)?)?)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex LabeledTimePattern = new(@"^(?<label>bereidingstijd|prep(?:aration)?(?:\s*time)?|voorbereidingstijd|kooktijd|baktijd|cook(?:ing)?(?:\s*time)?)\s*:?\s*(?<rest>.+)$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ServingsPattern = new(@"^(?:voor\s+)?(?<count>\d+)\s*(?:personen|persoon|porties|portie|servings|serving|stuks|stuk)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ServesPhrasePattern = new(@"^serves?\s+(?<count>\d+)(?:\s*(?:to|-|–)\s*\d+)?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex ServesInParenthesesPattern = new(@"\(\s*serves?\s+(?<count>\d+)(?:\s*(?:to|-|–)\s*\d+)?\s*\)", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DifficultyLabeledPattern = new(@"(?:moeilijkheidsgraad|difficulty|niveau)[^.\n]{0,60}:\s*(?<level>makkelijk|gemiddeld|moeilijk|easy|medium|hard|normaal)\b", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex DifficultyTrailingPattern = new(@"\b(?<level>makkelijk|gemiddeld|moeilijk|easy|medium|hard|normaal)\s*\.?$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Compiled);

    internal static bool TryParseLabeledTime(string line, out RecipeTimeKind kind, out int minutes)
    {
        kind = RecipeTimeKind.Cooking;
        minutes = 0;
        var labeled = LabeledTimePattern.Match(line.Trim());
        if (!labeled.Success) return false;
        var label = labeled.Groups["label"].Value.ToLowerInvariant();
        kind = label.StartsWith("bereid", StringComparison.Ordinal) || label.StartsWith("prep", StringComparison.Ordinal) || label.StartsWith("voorbereid", StringComparison.Ordinal) ? RecipeTimeKind.Preparation : RecipeTimeKind.Cooking;
        return TryParseTime(labeled.Groups["rest"].Value.Trim(), out minutes);
    }

    internal static bool TryParseTime(string line, out int minutes)
    {
        minutes = 0;
        var match = TimePattern.Match(line.Trim());
        if (!match.Success) return false;
        if (match.Groups["minutes"].Success)
        {
            minutes = int.Parse(match.Groups["minutes"].Value, CultureInfo.InvariantCulture);
            return minutes > 0;
        }
        var hours = int.Parse(match.Groups["hours"].Value, CultureInfo.InvariantCulture);
        var extra = match.Groups["minutes2"].Success ? int.Parse(match.Groups["minutes2"].Value, CultureInfo.InvariantCulture) : 0;
        minutes = hours * 60 + extra;
        return minutes > 0;
    }

    internal static bool TryParseDifficulty(string line, out int difficulty)
    {
        difficulty = 0;
        var normalized = line.Trim();
        if (normalized.Length == 0) return false;
        if (TryMapDifficultyWord(normalized.ToLowerInvariant(), out difficulty)) return true;
        var labeled = DifficultyLabeledPattern.Match(normalized);
        if (labeled.Success && TryMapDifficultyWord(labeled.Groups["level"].Value.ToLowerInvariant(), out difficulty)) return true;
        var trailing = DifficultyTrailingPattern.Match(normalized);
        return normalized.Length <= 120 && trailing.Success && TryMapDifficultyWord(trailing.Groups["level"].Value.ToLowerInvariant(), out difficulty) && (normalized.Contains(':', StringComparison.Ordinal) || normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length <= 4);
    }

    internal static bool TryParseServingsPhrase(string line, out int servings)
    {
        servings = 0;
        var trimmed = line.Trim().TrimEnd(':').Trim();
        if (trimmed.Length == 0) return false;
        var match = ServesPhrasePattern.Match(trimmed);
        if (!match.Success) match = ServesInParenthesesPattern.Match(trimmed);
        if (!match.Success) match = ServingsPattern.Match(trimmed);
        if (!match.Success) return false;
        servings = int.Parse(match.Groups["count"].Value, CultureInfo.InvariantCulture);
        return servings is > 0 and <= 100;
    }

    internal static string? NormalizeTitleCandidate(string line)
    {
        var trimmed = line.Trim();
        return trimmed.Any(char.IsLetter) && !trimmed.Any(char.IsLower)
            ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(trimmed.ToLowerInvariant())
            : trimmed;
    }

    internal static bool TryParseDifficultyLabel(string line, out string label)
    {
        label = string.Empty;
        if (!TryParseDifficulty(line, out var difficulty)) return false;
        label = difficulty switch
        {
            (int)Difficulty.Easy => "Makkelijk",
            (int)Difficulty.Medium => "Gemiddeld",
            (int)Difficulty.Hard => "Moeilijk",
            _ => string.Empty,
        };
        return label.Length > 0;
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
}
