using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

internal static class ShoppingListMapping
{
    public static GetOrCreateShoppingListGroupResult MapGroup(ShoppingListGroup group) =>
        new()
        {
            GroupId = group.Id,
            Lists = group.Lists
                .OrderBy(l => l.StoreOrder)
                .Select(MapList)
                .ToList(),
        };

    public static ShoppingListDto MapList(ShoppingList list) =>
        new()
        {
            Id = list.Id,
            Name = list.Name,
            StoreOrder = list.StoreOrder,
            Items = list.Items
                .OrderBy(i => i.SortOrder)
                .ThenBy(i => i.DisplayName)
                .Select(MapItem)
                .ToList(),
        };

    public static ShoppingListItemDto MapItem(ShoppingListItem item) =>
        new()
        {
            Id = item.Id,
            DisplayName = item.DisplayName,
            Preparation = item.Preparation,
            Quantity = item.Quantity?.Value,
            Unit = item.Unit?.ToString(),
            IsChecked = item.IsChecked,
            Sources = item.Sources
                .Select(s => new ShoppingListItemSourceDto
                {
                    RecipeId = s.RecipeId,
                    RecipeTitle = s.RecipeTitle,
                })
                .ToList(),
        };
}
