using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.ShoppingLists;

public sealed class ShoppingListIngredientLine
{
    public Guid? CanonicalIngredientId { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public string? Preparation { get; init; }

    public decimal Quantity { get; init; }

    public Unit Unit { get; init; }

    public Guid RecipeId { get; init; }

    public string RecipeTitle { get; init; } = string.Empty;
}
