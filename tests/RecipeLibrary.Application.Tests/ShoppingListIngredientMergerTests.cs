using RecipeLibrary.Application.Ingredients;
using Xunit;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.Tests;

public sealed class ShoppingListIngredientMergerTests
{
    private readonly ShoppingListIngredientMerger _merger =
        new(new IngredientTextNormalizer());

    [Fact]
    public void MergeIntoList_AddsQuantitiesForSameCanonicalIngredientAndUnit()
    {
        var listId = Guid.NewGuid();
        var canonicalId = Guid.NewGuid();
        var existing = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = listId,
            CanonicalIngredientId = canonicalId,
            DisplayName = "Tomato",
            Quantity = new Quantity(2),
            Unit = Unit.Gram,
            Sources =
            [
                new ShoppingListItemSource
                {
                    ShoppingListItemId = Guid.Empty,
                    RecipeId = Guid.NewGuid(),
                    RecipeTitle = "Soup",
                },
            ],
        };
        existing.Sources.First().ShoppingListItemId = existing.Id;

        var result = _merger.MergeIntoList(
            [existing],
            [
                new ShoppingListIngredientLine
                {
                    CanonicalIngredientId = canonicalId,
                    DisplayName = "Tomato",
                    Quantity = 3,
                    Unit = Unit.Gram,
                    RecipeId = Guid.NewGuid(),
                    RecipeTitle = "Salad",
                },
            ],
            listId);

        Assert.Single(result);
        Assert.Equal(5, result[0].Quantity.Value);
        Assert.Equal(2, result[0].Sources.Count);
    }

    [Fact]
    public void MergeIntoList_KeepsSeparateLinesWhenUnitDiffers()
    {
        var listId = Guid.NewGuid();
        var canonicalId = Guid.NewGuid();
        var existing = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = listId,
            CanonicalIngredientId = canonicalId,
            DisplayName = "Milk",
            Quantity = new Quantity(1),
            Unit = Unit.Piece,
            Sources = [],
        };

        var result = _merger.MergeIntoList(
            [existing],
            [
                new ShoppingListIngredientLine
                {
                    CanonicalIngredientId = canonicalId,
                    DisplayName = "Milk",
                    Quantity = 500,
                    Unit = Unit.Milliliter,
                    RecipeId = Guid.NewGuid(),
                    RecipeTitle = "Sauce",
                },
            ],
            listId);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeIntoList_DoesNotDuplicateSourceForSameRecipe()
    {
        var listId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var existing = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = listId,
            DisplayName = "Salt",
            Quantity = new Quantity(1),
            Unit = Unit.Gram,
            Sources =
            [
                new ShoppingListItemSource
                {
                    ShoppingListItemId = Guid.Empty,
                    RecipeId = recipeId,
                    RecipeTitle = "Soup",
                },
            ],
        };
        existing.Sources.First().ShoppingListItemId = existing.Id;

        var result = _merger.MergeIntoList(
            [existing],
            [
                new ShoppingListIngredientLine
                {
                    DisplayName = "Salt",
                    Quantity = 2,
                    Unit = Unit.Gram,
                    RecipeId = recipeId,
                    RecipeTitle = "Soup",
                },
            ],
            listId);

        Assert.Single(result);
        Assert.Equal(1, result[0].Quantity.Value);
        Assert.Single(result[0].Sources);
    }
}
