using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Pantry;

/// <summary>
/// Removes shopping-list lines/items that match pantry staples (presence only).
/// </summary>
public sealed class PantryExclusionFilter(PantryIngredientMerger merger)
{
    public IReadOnlyList<ShoppingListIngredientLine> ExcludeMatchingLines(
        IReadOnlyList<ShoppingListIngredientLine> lines,
        IReadOnlyList<PantryItem> pantryItems)
    {
        if (pantryItems.Count == 0)
        {
            return lines;
        }

        return lines
            .Where(line => !merger.IsPresent(pantryItems, line.CanonicalIngredientId, line.DisplayName))
            .ToList();
    }

    public IReadOnlyList<ShoppingListItem> ExcludeMatchingItems(
        IReadOnlyList<ShoppingListItem> items,
        IReadOnlyList<PantryItem> pantryItems)
    {
        if (pantryItems.Count == 0)
        {
            return items;
        }

        return items
            .Where(item => !merger.IsPresent(pantryItems, item.CanonicalIngredientId, item.DisplayName))
            .ToList();
    }
}
