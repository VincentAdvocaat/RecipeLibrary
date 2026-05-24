namespace RecipeLibrary.Application.Ingredients;

public static class IngredientListDisplay
{
    public static string FormatNameWithPreparation(string name, string? preparation)
    {
        var trimmedName = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(preparation))
        {
            return trimmedName;
        }

        return string.IsNullOrEmpty(trimmedName)
            ? preparation.Trim()
            : $"{trimmedName} ({preparation.Trim()})";
    }
}
