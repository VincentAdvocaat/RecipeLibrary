namespace RecipeLibrary.Application.Pantry;

/// <summary>
/// Presence identity: canonical id when set, otherwise normalized display name.
/// Equality is handled by <see cref="PantryIngredientMerger.Matches"/> (not by this struct alone).
/// </summary>
public readonly record struct PantryMergeKey(
    Guid? CanonicalIngredientId,
    string NormalizedDisplayName);
