namespace RecipeLibrary.Application.Ingredients;

public static class IngredientCandidateMatcher
{
    public static bool Matches(string normalizedQuery, string normalizedName)
    {
        if (string.IsNullOrWhiteSpace(normalizedQuery) || string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        if (normalizedName.Contains(normalizedQuery, StringComparison.Ordinal)
            || normalizedQuery.Contains(normalizedName, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var token in IngredientNameTokenizer.SplitTokens(normalizedQuery))
        {
            if (normalizedName.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
