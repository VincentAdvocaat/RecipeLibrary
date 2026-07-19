using System.Globalization;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Ingredients;

/// <summary>
/// Formats stored quantity+unit for display, optionally applying Mass↔Mass presentation.
/// Does not mutate stored recipe data unless callers explicitly persist the result
/// (e.g. import draft → editor VM).
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

    /// <summary>
    /// Remaps mass quantity/unit strings to the user's measure preference (import → editor).
    /// Kitchen and count units are left unchanged.
    /// </summary>
    public static (decimal? Quantity, string? UnitName) ApplyMassPreferenceToImported(
        decimal? quantity,
        string? unitName,
        MeasureSystem measureSystem)
    {
        if (quantity is null || string.IsNullOrWhiteSpace(unitName))
        {
            return (quantity, string.IsNullOrWhiteSpace(unitName) ? null : unitName);
        }

        if (!UnitRules.TryParse(unitName, out var unit) || unit == Unit.Unknown || !UnitClassification.IsMass(unit))
        {
            return (quantity, unitName);
        }

        var (displayQty, displayUnit) = ApplyMassPresentation(quantity.Value, unit, measureSystem);
        var normalized = IngredientQuantityFormatter.Normalize(displayQty, displayUnit);
        return (normalized, displayUnit.ToString());
    }

    public static string FormatQuantity(decimal quantity, Unit unit) =>
        IngredientQuantityFormatter.Format(quantity, unit, CultureInfo.CurrentCulture);
}
