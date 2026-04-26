namespace RecipeLibrary.Application.Ingredients;

public sealed class IngredientNameParser
{
    private static readonly string[] PreparationKeywords =
    [
        "gesneden",
        "geraspt",
        "fijngehakt",
        "gepeld"
    ];

    public ParsedIngredient ParseIngredient(string? input)
    {
        var value = (input ?? string.Empty).Trim();
        if (value.Length == 0)
        {
            return new ParsedIngredient(string.Empty, null);
        }

        foreach (var keyword in PreparationKeywords)
        {
            if (value.EndsWith($" {keyword}", StringComparison.OrdinalIgnoreCase))
            {
                var name = value[..^($" {keyword}".Length)].Trim();
                return new ParsedIngredient(name, keyword);
            }
        }

        return new ParsedIngredient(value, null);
    }
}

public sealed record ParsedIngredient(string Name, string? Preparation);
