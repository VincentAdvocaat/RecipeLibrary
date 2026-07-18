using System.Globalization;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Formats stored quantity+unit for display, optionally applying Mass↔Mass presentation.
/// Does not mutate stored recipe data.
/// </summary>
public static class IngredientMeasurePresenter
{
    private const decimal PoundThresholdGrams = 453.59237m;

    public static (decimal Quantity, Unit Unit) ApplyMassPresentation(
        decimal quantity,
        Unit unit,
        MeasureSystem measureSystem)
    {
        if (!UnitClassification.IsMass(unit))
        {
            return (quantity, unit);
        }

        var grams = MassUnitConverter.ToGrams(quantity, unit);

        if (measureSystem == MeasureSystem.Metric)
        {
            return (decimal.Round(grams, 0, MidpointRounding.AwayFromZero), Unit.Gram);
        }

        if (grams >= PoundThresholdGrams)
        {
            var pounds = MassUnitConverter.FromGrams(grams, Unit.Pound);
            return (decimal.Round(pounds, 2, MidpointRounding.AwayFromZero), Unit.Pound);
        }

        var ounces = MassUnitConverter.FromGrams(grams, Unit.Ounce);
        return (decimal.Round(ounces, 2, MidpointRounding.AwayFromZero), Unit.Ounce);
    }

    public static string FormatQuantity(decimal quantity, Unit unit) =>
        IngredientQuantityFormatter.Format(quantity, unit, CultureInfo.CurrentCulture);
}
