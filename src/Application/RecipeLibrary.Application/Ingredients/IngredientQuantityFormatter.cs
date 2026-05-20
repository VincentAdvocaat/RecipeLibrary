using System.Globalization;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Normalizes and formats ingredient quantities as whole numbers (invariant, no decimal separator).
/// </summary>
public static class IngredientQuantityFormatter
{
    public static decimal Normalize(decimal quantity, Unit unit) =>
        decimal.Round(quantity, 0, MidpointRounding.AwayFromZero);

    public static string Format(decimal quantity, Unit unit) =>
        ((long)Normalize(quantity, unit)).ToString(CultureInfo.InvariantCulture);

    public static void ValidateQuantity(decimal quantity, Unit unit)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Ingredient quantity must be greater than 0.");
        }

        var normalized = Normalize(quantity, unit);
        if (normalized <= 0)
        {
            throw new ArgumentException("Ingredient quantity must be greater than 0.");
        }

        if (quantity != normalized)
        {
            throw new ArgumentException(
                $"Quantity must be a whole number for unit '{unit}'.");
        }
    }
}
