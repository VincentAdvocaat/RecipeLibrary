using System.Globalization;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Components.Helpers;

public static class ShoppingListDisplayHelper
{
    public static string FormatQuantityLine(
        ShoppingListItemDto item,
        Func<string, string> localizeUnit,
        MeasureSystem measureSystem = MeasureSystem.Metric)
    {
        if (item.Quantity is null || string.IsNullOrWhiteSpace(item.Unit))
        {
            return string.Empty;
        }

        var unit = UnitRules.TryParse(item.Unit, out var parsed) ? parsed : Unit.Unknown;
        if (unit == Unit.Unknown)
        {
            var fallback = ((long)decimal.Round(item.Quantity.Value, 0, MidpointRounding.AwayFromZero))
                .ToString(CultureInfo.CurrentCulture);
            return $"{fallback} {localizeUnit(item.Unit)}";
        }

        var (displayQty, displayUnit) = IngredientMeasurePresenter.ApplyMassPresentation(
            item.Quantity.Value,
            unit,
            measureSystem);
        var quantityText = IngredientQuantityFormatter.Format(displayQty, displayUnit);
        var unitLabel = localizeUnit(displayUnit.ToString());
        return $"{quantityText} {unitLabel}";
    }

    public static string FormatSourceTitles(IReadOnlyList<ShoppingListItemSourceDto> sources) =>
        string.Join(", ", sources.Select(s => s.RecipeTitle));
}
