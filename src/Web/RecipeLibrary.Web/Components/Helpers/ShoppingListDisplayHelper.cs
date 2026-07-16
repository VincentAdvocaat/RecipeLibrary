using System.Globalization;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Components.Helpers;

public static class ShoppingListDisplayHelper
{
    public static string FormatQuantityLine(
        ShoppingListItemDto item,
        Func<string, string> localizeUnit)
    {
        if (item.Quantity is null || string.IsNullOrWhiteSpace(item.Unit))
        {
            return string.Empty;
        }

        var unit = UnitRules.TryParse(item.Unit, out var parsed) ? parsed : Unit.Unknown;
        var quantityText = unit == Unit.Unknown
            ? ((long)decimal.Round(item.Quantity.Value, 0, MidpointRounding.AwayFromZero))
                .ToString(CultureInfo.InvariantCulture)
            : IngredientQuantityFormatter.Format(item.Quantity.Value, unit);
        var unitLabel = localizeUnit(item.Unit);
        return $"{quantityText} {unitLabel}";
    }

    public static string FormatSourceTitles(IReadOnlyList<ShoppingListItemSourceDto> sources) =>
        string.Join(", ", sources.Select(s => s.RecipeTitle));
}
