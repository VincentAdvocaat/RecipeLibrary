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
    ];

    public static (string Name, string? Preparation) Split(string? input)
    {
        var value = (input ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return (string.Empty, null);
        }

        var commaIndex = value.IndexOf(',');
        if (commaIndex >= 0)
        {
            var name = value[..commaIndex].Trim().TrimEnd(',', ';').Trim();
            var preparation = value[(commaIndex + 1)..].Trim();
            if (preparation.Length == 0)
            {
                return SplitAffixes(name);
            }

            var (strippedName, _) = SplitAffixes(name);
            return (strippedName.Length > 0 ? strippedName : name, preparation);
        }

        return SplitAffixes(value);
    }

    private static (string Name, string? Preparation) SplitAffixes(string value)
    {
        // "verse basilicum" → name basilicum, prep vers
        if (value.StartsWith("verse ", StringComparison.OrdinalIgnoreCase))
        {
            return (value["verse ".Length..].Trim(), "vers");
        }

        if (value.StartsWith("vers ", StringComparison.OrdinalIgnoreCase))
        {
            return (value["vers ".Length..].Trim(), "vers");
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
}
