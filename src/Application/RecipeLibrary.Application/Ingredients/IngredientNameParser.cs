namespace RecipeLibrary.Application.Ingredients;

public sealed class IngredientNameParser
{
    private static readonly string[] PreparationPhrases =
    [
        "fijn gesneden",
        "in blokjes",
        "in reepjes",
        "fijngehakt",
        "gesneden",
        "geraspt",
        "gepeld",
    ];

    public ParsedIngredient ParseIngredient(string? input)
    {
        var value = (input ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return new ParsedIngredient(string.Empty, null);
        }

        foreach (var phrase in PreparationPhrases)
        {
            var suffix = $" {phrase}";
            if (value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                var name = value[..^suffix.Length].Trim();
                return new ParsedIngredient(name, phrase);
            }
        }

        return new ParsedIngredient(value, null);
    }
}

public sealed record ParsedIngredient(string Name, string? Preparation);
