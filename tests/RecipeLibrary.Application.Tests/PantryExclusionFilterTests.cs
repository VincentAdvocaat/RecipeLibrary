using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class PantryExclusionFilterTests
{
    private readonly PantryExclusionFilter _filter =
        new(new PantryIngredientMerger(new IngredientTextNormalizer()));

    [Fact]
    public void ExcludeMatchingLines_OmitsLine_WhenCanonicalIdMatches()
    {
        var canonicalId = Guid.NewGuid();
        var pantry = new[]
        {
            new PantryItem
            {
                Id = Guid.NewGuid(),
                DisplayName = "Zout",
                CanonicalIngredientId = canonicalId,
            },
        };

        var lines = new[]
        {
            new ShoppingListIngredientLine
            {
                CanonicalIngredientId = canonicalId,
                DisplayName = "Zout",
                Quantity = 1,
                Unit = Unit.Teaspoon,
                RecipeId = Guid.NewGuid(),
                RecipeTitle = "Test",
            },
            new ShoppingListIngredientLine
            {
                DisplayName = "Pasta",
                Quantity = 500,
                Unit = Unit.Gram,
                RecipeId = Guid.NewGuid(),
                RecipeTitle = "Test",
            },
        };

        var result = _filter.ExcludeMatchingLines(lines, pantry);

        Assert.Single(result);
        Assert.Equal("Pasta", result[0].DisplayName);
    }

    [Fact]
    public void ExcludeMatchingLines_OmitsLine_WhenNormalizedNameMatches()
    {
        var pantry = new[]
        {
            new PantryItem
            {
                Id = Guid.NewGuid(),
                DisplayName = "Zout",
            },
        };

        var lines = new[]
        {
            new ShoppingListIngredientLine
            {
                DisplayName = "zout",
                Quantity = 1,
                Unit = Unit.Teaspoon,
                RecipeId = Guid.NewGuid(),
                RecipeTitle = "Test",
            },
        };

        var result = _filter.ExcludeMatchingLines(lines, pantry);

        Assert.Empty(result);
    }

    [Fact]
    public void ExcludeMatchingLines_OmitsLine_WhenNormalizedNameMatches_DespiteDifferentCanonicalIds()
    {
        var pantry = new[]
        {
            new PantryItem
            {
                Id = Guid.NewGuid(),
                DisplayName = "Zout",
                CanonicalIngredientId = Guid.NewGuid(),
            },
        };

        var lines = new[]
        {
            new ShoppingListIngredientLine
            {
                CanonicalIngredientId = Guid.NewGuid(),
                DisplayName = "zout",
                Quantity = 1,
                Unit = Unit.Teaspoon,
                RecipeId = Guid.NewGuid(),
                RecipeTitle = "Test",
            },
        };

        var result = _filter.ExcludeMatchingLines(lines, pantry);

        Assert.Empty(result);
    }

    [Fact]
    public void ExcludeMatchingItems_KeepsNonMatchingItems()
    {
        var pantry = new[]
        {
            new PantryItem { Id = Guid.NewGuid(), DisplayName = "Olie" },
        };

        var items = new[]
        {
            new ShoppingListItem
            {
                Id = Guid.NewGuid(),
                DisplayName = "Olie",
                Quantity = new Quantity(1),
                Unit = Unit.Tablespoon,
            },
            new ShoppingListItem
            {
                Id = Guid.NewGuid(),
                DisplayName = "Ui",
                Quantity = new Quantity(1),
                Unit = Unit.Piece,
            },
        };

        var result = _filter.ExcludeMatchingItems(items, pantry);

        Assert.Single(result);
        Assert.Equal("Ui", result[0].DisplayName);
    }
}
