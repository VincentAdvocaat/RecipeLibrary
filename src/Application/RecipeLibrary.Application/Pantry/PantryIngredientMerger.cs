using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Pantry;

public sealed class PantryIngredientMerger(IIngredientTextNormalizer normalizer)
{
    /// <summary>
    /// Ensures the staple is present. Idempotent: returns the existing item when it already matches.
    /// </summary>
    public PantryItem EnsurePresent(
        IReadOnlyList<PantryItem> existingItems,
        Guid? canonicalIngredientId,
        string displayName,
        string ownerKey)
    {
        var existing = FindMatch(existingItems, canonicalIngredientId, displayName);
        if (existing is not null)
        {
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            return existing;
        }

        var now = DateTimeOffset.UtcNow;
        return new PantryItem
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerKey,
            CanonicalIngredientId = canonicalIngredientId,
            DisplayName = displayName.Trim(),
            CreatedAt = now,
            UpdatedAt = now,
        };
    }

    public bool IsPresent(
        IReadOnlyList<PantryItem> pantryItems,
        Guid? canonicalIngredientId,
        string displayName) =>
        FindMatch(pantryItems, canonicalIngredientId, displayName) is not null;

    public PantryMergeKey BuildKey(Guid? canonicalIngredientId, string displayName) =>
        new(canonicalIngredientId, normalizer.Normalize(displayName));

    internal PantryMergeKey BuildKey(PantryItem item) =>
        BuildKey(item.CanonicalIngredientId, item.DisplayName);

    public bool Matches(
        Guid? leftCanonicalId,
        string leftDisplayName,
        Guid? rightCanonicalId,
        string rightDisplayName)
    {
        if (leftCanonicalId.HasValue && rightCanonicalId.HasValue)
        {
            return leftCanonicalId.Value == rightCanonicalId.Value;
        }

        return string.Equals(
            normalizer.Normalize(leftDisplayName),
            normalizer.Normalize(rightDisplayName),
            StringComparison.Ordinal);
    }

    private PantryItem? FindMatch(
        IReadOnlyList<PantryItem> items,
        Guid? canonicalIngredientId,
        string displayName)
    {
        foreach (var item in items)
        {
            if (Matches(
                    item.CanonicalIngredientId,
                    item.DisplayName,
                    canonicalIngredientId,
                    displayName))
            {
                return item;
            }
        }

        return null;
    }
}
