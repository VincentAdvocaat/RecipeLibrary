using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Pantry;

public readonly record struct PantryMergeKey(
    Guid? CanonicalIngredientId,
    string NormalizedDisplayName,
    Unit Unit);
