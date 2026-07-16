using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.UseCases.Pantry;

internal static class PantryMapping
{
    public static PantryItemDto Map(PantryItem item) =>
        new()
        {
            Id = item.Id,
            CanonicalIngredientId = item.CanonicalIngredientId,
            DisplayName = item.DisplayName,
        };

    public static GetPantryItemsResult MapItems(IReadOnlyList<PantryItem> items) =>
        new() { Items = items.Select(Map).ToList() };
}
