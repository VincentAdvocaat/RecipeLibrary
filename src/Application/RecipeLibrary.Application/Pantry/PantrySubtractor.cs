using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Pantry;

public sealed class PantrySubtractor(PantryIngredientMerger merger)
{
    public IReadOnlyList<ShoppingListIngredientLine> SubtractFromLines(
        IReadOnlyList<ShoppingListIngredientLine> lines,
        IReadOnlyList<PantryItem> pantryItems)
    {
        var pantryByKey = pantryItems
            .GroupBy(merger.BuildKey)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Quantity.Value));

        var result = new List<ShoppingListIngredientLine>();

        foreach (var line in lines)
        {
            var key = merger.BuildKey(line.CanonicalIngredientId, line.DisplayName, line.Unit);
            var pantryQty = pantryByKey.GetValueOrDefault(key, 0m);
            var remaining = IngredientQuantityFormatter.Normalize(line.Quantity - pantryQty, line.Unit);

            if (remaining <= 0)
            {
                continue;
            }

            result.Add(new ShoppingListIngredientLine
            {
                CanonicalIngredientId = line.CanonicalIngredientId,
                DisplayName = line.DisplayName,
                Preparation = line.Preparation,
                Quantity = remaining,
                Unit = line.Unit,
                RecipeId = line.RecipeId,
                RecipeTitle = line.RecipeTitle,
            });
        }

        return result;
    }

    public IReadOnlyList<ShoppingListItem> SubtractFromListItems(
        IReadOnlyList<ShoppingListItem> items,
        IReadOnlyList<PantryItem> pantryItems)
    {
        var pantryByKey = pantryItems
            .GroupBy(merger.BuildKey)
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Quantity.Value));

        var result = new List<ShoppingListItem>();

        foreach (var item in items)
        {
            var key = merger.BuildKey(item.CanonicalIngredientId, item.DisplayName, item.Unit);
            var pantryQty = pantryByKey.GetValueOrDefault(key, 0m);
            var remaining = IngredientQuantityFormatter.Normalize(item.Quantity.Value - pantryQty, item.Unit);

            if (remaining <= 0)
            {
                continue;
            }

            var clone = CloneItem(item);
            clone.Quantity = new Quantity(remaining);
            result.Add(clone);
        }

        return result;
    }

    private static ShoppingListItem CloneItem(ShoppingListItem source) =>
        new()
        {
            Id = source.Id,
            ShoppingListId = source.ShoppingListId,
            CanonicalIngredientId = source.CanonicalIngredientId,
            DisplayName = source.DisplayName,
            Preparation = source.Preparation,
            Quantity = source.Quantity,
            Unit = source.Unit,
            IsChecked = source.IsChecked,
            SortOrder = source.SortOrder,
            Sources = source.Sources
                .Select(s => new ShoppingListItemSource
                {
                    ShoppingListItemId = s.ShoppingListItemId,
                    RecipeId = s.RecipeId,
                    RecipeTitle = s.RecipeTitle,
                })
                .ToList(),
        };
}
