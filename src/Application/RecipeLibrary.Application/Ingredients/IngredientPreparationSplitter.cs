using System.Text.RegularExpressions;

namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Shared name/preparation splitting for import and UI blur (comma + known suffix/prefix phrases).
/// </summary>
public static class IngredientPreparationSplitter
{
    private static readonly string[] PreparationPhrases =
    [
        "fijn gesneden",
        "van goede kwaliteit",
        "in blokjes",
        "in reepjes",
        "fijngehakt",
        "gesneden",
        "geraspt",
        "gepeld",
        "finely chopped",
        "chopped",
        "crushed",
        "drained and rinsed",
        "for drizzling",
        "for garnish",
        "to taste",
        "adjust to taste",
    ];

    private static readonly string[] LeadingPrepWords =
    [
        "chopped",
        "crushed",
        "minced",
        "grated",
        "sliced",
    ];

    public static (string Name, string? Preparation) Split(string? input)
    {
        var value = (input ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return (string.Empty, null);
        }

        if (value.StartsWith("Additional ", StringComparison.OrdinalIgnoreCase))
        {
            value = value["Additional ".Length..].Trim();
        }

        // Only peel parentheses that close at the end (aliases / notes).
        var trailingParenPrep = TryExtractTrailingParentheses(ref value);
        var commaIndex = IndexOfCommaOutsideParentheses(value);
        if (commaIndex >= 0)
        {
            var name = value[..commaIndex].Trim().TrimEnd(',', ';').Trim();
            var preparation = value[(commaIndex + 1)..].Trim();
            var nameParen = TryExtractTrailingParentheses(ref name);
            if (preparation.Length == 0)
            {
                var (affixName, affixPrep) = SplitAffixes(name);
                return (affixName, MergePrep(MergePrep(nameParen, affixPrep), trailingParenPrep));
            }

            var (strippedName, _) = SplitAffixes(name);
            return (
                strippedName.Length > 0 ? strippedName : name,
                MergePrep(MergePrep(nameParen, preparation), trailingParenPrep));
        }

        var (baseName, affix) = SplitAffixes(value);
        return (baseName, MergePrep(affix, trailingParenPrep));
    }

    private static string? TryExtractTrailingParentheses(ref string value)
    {
        var open = value.LastIndexOf('(');
        var close = value.LastIndexOf(')');
        if (open < 0 || close <= open || close != value.Length - 1)
        {
            return null;
        }

        var inside = value[(open + 1)..close].Trim();
        value = value[..open].Trim().TrimEnd(',', ';').Trim();
        return inside.Length > 0 ? inside : null;
    }

    private static int IndexOfCommaOutsideParentheses(string value)
    {
        var depth = 0;
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (c == ',' && depth == 0)
            {
                return i;
            }
        }

        return -1;
    }

    private static (string Name, string? Preparation) SplitAffixes(string value)
    {
        if (value.StartsWith("verse ", StringComparison.OrdinalIgnoreCase))
        {
            return (value["verse ".Length..].Trim(), "vers");
        }

        if (value.StartsWith("vers ", StringComparison.OrdinalIgnoreCase))
        {
            return (value["vers ".Length..].Trim(), "vers");
        }

        foreach (var leading in LeadingPrepWords)
        {
            var prefix = leading + " ";
            if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return (value[prefix.Length..].Trim(), leading);
            }
        }

        foreach (var phrase in PreparationPhrases)
        {
            var suffix = $" {phrase}";
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var name = value[..^suffix.Length].Trim().TrimEnd(',', ';').Trim();
                return (name, phrase);
            }
        }

        return (value.TrimEnd(',', ';').Trim(), null);
    }

    private static string? MergePrep(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return string.IsNullOrWhiteSpace(right) ? null : right.Trim();
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left.Trim();
        }

        return $"{left.Trim()}, {right.Trim()}";
    }
}
