namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Unmeasured ingredients (e.g. olie/zout naar smaak) have neither quantity nor unit.
/// </summary>
public static class IngredientMeasure
{
    public static bool IsUnmeasured(decimal? quantity, string? unit)
    {
        var noUnit = string.IsNullOrWhiteSpace(unit);
        var noQuantity = quantity is null or <= 0;
        return noUnit && noQuantity;
    }
}
