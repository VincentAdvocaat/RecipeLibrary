using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Pantry;

public sealed class PantryIngredientMerger(IIngredientTextNormalizer normalizer)
{
    public PantryItem MergeLineIntoPantry(
        IReadOnlyList<PantryItem> existingItems,
        Guid? canonicalIngredientId,
        string displayName,
        decimal quantity,
        Unit unit,
        string ownerKey)
    {
        var items = existingItems.ToList();
        var key = BuildKey(canonicalIngredientId, displayName, unit);
        var normalizedQuantity = IngredientQuantityFormatter.Normalize(quantity, unit);
        var now = DateTimeOffset.UtcNow;

        var existing = FindMatch(items, key);
        if (existing is not null)
        {
            existing.Quantity = new Quantity(
                IngredientQuantityFormatter.Normalize(
                    existing.Quantity.Value + normalizedQuantity,
                    existing.Unit));
            existing.UpdatedAt = now;
            return existing;
        }

        var item = new PantryItem
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerKey,
            CanonicalIngredientId = canonicalIngredientId,
            DisplayName = displayName.Trim(),
            Quantity = new Quantity(normalizedQuantity),
            Unit = unit,
            CreatedAt = now,
            UpdatedAt = now,
        };
        items.Add(item);
        return item;
    }

    internal PantryMergeKey BuildKey(PantryItem item) =>
        BuildKey(item.CanonicalIngredientId, item.DisplayName, item.Unit);

    public PantryMergeKey BuildKey(Guid? canonicalIngredientId, string displayName, Unit unit) =>
        new(
            canonicalIngredientId,
            normalizer.Normalize(displayName),
            unit);

    private PantryItem? FindMatch(IReadOnlyList<PantryItem> items, PantryMergeKey key)
    {
        foreach (var item in items)
        {
            if (BuildKey(item).Equals(key))
            {
                return item;
            }
        }

        return null;
    }
}
