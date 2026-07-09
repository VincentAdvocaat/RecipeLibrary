namespace RecipeLibrary.Application.Ingredients;

public static class IngredientNameTokenizer
{
    public static IReadOnlyList<string> SplitTokens(string normalizedName) =>
        normalizedName.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
