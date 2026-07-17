using System.Globalization;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Normalizes and formats ingredient quantities (whole numbers, culinary fractions, or decimals).
/// </summary>
public static class IngredientQuantityFormatter
{
    private const int DecimalQuantityScale = 2;

    public static decimal Normalize(decimal quantity, Unit unit)
    {
        if (UnitRules.AllowsCulinaryFractions(unit))
        {
            return CulinaryQuantityFractions.SnapToNearest(quantity);
        }

        if (UnitRules.AllowsDecimalQuantity(unit))
        {
            return decimal.Round(quantity, DecimalQuantityScale, MidpointRounding.AwayFromZero);
        }

        return decimal.Round(quantity, 0, MidpointRounding.AwayFromZero);
    }

    public static string Format(decimal quantity, Unit unit)
    {
        var normalized = Normalize(quantity, unit);
        if (UnitRules.AllowsCulinaryFractions(unit))
        {
            return CulinaryQuantityFractions.FormatMixed(normalized);
        }

        if (UnitRules.AllowsDecimalQuantity(unit))
        {
            return normalized.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return ((long)normalized).ToString(CultureInfo.InvariantCulture);
    }

    public static void ValidateQuantity(decimal quantity, Unit unit)
    {
        if (quantity <= 0)
        {
            throw new ArgumentException("Ingredient quantity must be greater than 0.");
        }

        if (UnitRules.AllowsCulinaryFractions(unit))
        {
            if (!CulinaryQuantityFractions.TrySnap(quantity, out var snapped) || snapped <= 0)
            {
                throw new ArgumentException(
                    $"Quantity must be a culinary fraction (¼, ⅓, ½, ⅔, ¾) for unit '{unit}'.");
            }

            return;
        }

        var normalized = Normalize(quantity, unit);
        if (normalized <= 0)
        {
            throw new ArgumentException("Ingredient quantity must be greater than 0.");
        }

        if (quantity != normalized)
        {
            if (UnitRules.AllowsDecimalQuantity(unit))
            {
                throw new ArgumentException(
                    $"Quantity must have at most {DecimalQuantityScale} decimal places for unit '{unit}'.");
            }

            throw new ArgumentException(
                $"Quantity must be a whole number for unit '{unit}'.");
        }
    }
}
