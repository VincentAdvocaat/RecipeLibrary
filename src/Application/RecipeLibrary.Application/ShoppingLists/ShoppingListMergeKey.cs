using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.ShoppingLists;

internal readonly record struct ShoppingListMergeKey(
    Guid? CanonicalIngredientId,
    string NormalizedDisplayName,
    Unit Unit,
    string? Preparation);
