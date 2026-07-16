using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.ShoppingLists;

public sealed class ShoppingListIngredientMerger(IIngredientTextNormalizer normalizer)
{
    public IReadOnlyList<ShoppingListItem> MergeIntoList(
        IReadOnlyList<ShoppingListItem> existingItems,
        IReadOnlyList<ShoppingListIngredientLine> newLines,
        Guid shoppingListId)
    {
        var items = existingItems.Select(CloneItem).ToList();
        var sortBase = items.Count > 0 ? items.Max(i => i.SortOrder) + 1 : 0;

        foreach (var line in newLines)
        {
            var key = BuildKey(line);
            var existing = FindMatch(items, key);

            if (existing is not null)
            {
                if (!HasSource(existing, line.RecipeId))
                {
                    existing.Quantity = SumQuantities(existing.Quantity, line.Quantity, existing.Unit);
                    existing.Sources.Add(new ShoppingListItemSource
                    {
                        ShoppingListItemId = existing.Id,
                        RecipeId = line.RecipeId,
                        RecipeTitle = line.RecipeTitle,
                    });
                }
            }
            else
            {
                var item = new ShoppingListItem
                {
                    Id = Guid.NewGuid(),
                    ShoppingListId = shoppingListId,
                    CanonicalIngredientId = line.CanonicalIngredientId,
                    DisplayName = line.DisplayName,
                    Preparation = line.Preparation,
                    Quantity = NormalizeLineQuantity(line.Quantity, line.Unit),
                    Unit = line.Unit,
                    IsChecked = false,
                    SortOrder = sortBase++,
                    Sources =
                    [
                        new ShoppingListItemSource
                        {
                            ShoppingListItemId = Guid.Empty,
                            RecipeId = line.RecipeId,
                            RecipeTitle = line.RecipeTitle,
                        },
                    ],
                };
                item.Sources.First().ShoppingListItemId = item.Id;
                items.Add(item);
            }
        }

        return items;
    }

    public IReadOnlyList<ShoppingListItem> MergeManualLineIntoList(
        IReadOnlyList<ShoppingListItem> existingItems,
        Guid? canonicalIngredientId,
        string displayName,
        string? preparation,
        decimal? quantity,
        Unit? unit,
        Guid shoppingListId)
    {
        var items = existingItems.Select(CloneItem).ToList();
        var key = new ShoppingListMergeKey(
            canonicalIngredientId,
            normalizer.Normalize(displayName),
            unit,
            NormalizePreparation(preparation));

        var existing = FindMatch(items, key);
        var normalizedQuantity = NormalizeLineQuantity(quantity, unit);

        if (existing is not null)
        {
            existing.Quantity = SumQuantities(existing.Quantity, normalizedQuantity?.Value, existing.Unit);
            return items;
        }

        var sortOrder = items.Count > 0 ? items.Max(i => i.SortOrder) + 1 : 0;
        items.Add(new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = shoppingListId,
            CanonicalIngredientId = canonicalIngredientId,
            DisplayName = displayName.Trim(),
            Preparation = NormalizePreparation(preparation),
            Quantity = normalizedQuantity,
            Unit = unit,
            IsChecked = false,
            SortOrder = sortOrder,
        });

        return items;
    }

    private ShoppingListMergeKey BuildKey(ShoppingListItem item) =>
        new(
            item.CanonicalIngredientId,
            normalizer.Normalize(item.DisplayName),
            item.Unit,
            NormalizePreparation(item.Preparation));

    private ShoppingListMergeKey BuildKey(ShoppingListIngredientLine line) =>
        new(
            line.CanonicalIngredientId,
            normalizer.Normalize(line.DisplayName),
            line.Unit,
            NormalizePreparation(line.Preparation));

    private ShoppingListItem? FindMatch(IReadOnlyList<ShoppingListItem> items, ShoppingListMergeKey key)
    {
        foreach (var item in items)
        {
            if (BuildKeyFromItem(item).Equals(key))
            {
                return item;
            }
        }

        return null;
    }

    private ShoppingListMergeKey BuildKeyFromItem(ShoppingListItem item) => BuildKey(item);

    private static bool HasSource(ShoppingListItem item, Guid recipeId) =>
        item.Sources.Any(s => s.RecipeId == recipeId);

    private static string? NormalizePreparation(string? preparation)
    {
        var trimmed = (preparation ?? string.Empty).Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static Quantity? NormalizeLineQuantity(decimal? quantity, Unit? unit)
    {
        if (unit is null || quantity is null)
        {
            return null;
        }

        return new Quantity(IngredientQuantityFormatter.Normalize(quantity.Value, unit.Value));
    }

    private static Quantity? SumQuantities(Quantity? existing, decimal? add, Unit? unit)
    {
        if (unit is null)
        {
            return existing;
        }

        if (existing is null)
        {
            return add is null ? null : new Quantity(IngredientQuantityFormatter.Normalize(add.Value, unit.Value));
        }

        if (add is null)
        {
            return existing;
        }

        return new Quantity(
            IngredientQuantityFormatter.Normalize(existing.Value.Value + add.Value, unit.Value));
    }

    public IReadOnlyList<ShoppingListItem> MergeItemIntoList(
        IReadOnlyList<ShoppingListItem> existingItems,
        ShoppingListItem itemToMove,
        Guid targetShoppingListId)
    {
        var items = existingItems.Select(CloneItem).ToList();
        var key = BuildKey(itemToMove);
        var existing = FindMatch(items, key);

        if (existing is not null)
        {
            existing.Quantity = SumQuantities(
                existing.Quantity,
                itemToMove.Quantity?.Value,
                existing.Unit);

            foreach (var source in itemToMove.Sources)
            {
                if (!HasSource(existing, source.RecipeId))
                {
                    existing.Sources.Add(new ShoppingListItemSource
                    {
                        ShoppingListItemId = existing.Id,
                        RecipeId = source.RecipeId,
                        RecipeTitle = source.RecipeTitle,
                    });
                }
            }
        }
        else
        {
            var clone = CloneItem(itemToMove);
            clone.Id = Guid.NewGuid();
            clone.ShoppingListId = targetShoppingListId;
            clone.IsChecked = false;
            clone.SortOrder = items.Count > 0 ? items.Max(i => i.SortOrder) + 1 : 0;
            foreach (var source in clone.Sources)
            {
                source.ShoppingListItemId = clone.Id;
            }

            items.Add(clone);
        }

        return items;
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
