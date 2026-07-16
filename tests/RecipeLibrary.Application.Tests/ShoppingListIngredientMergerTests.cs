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
        Assert.Equal(5, result[0].Quantity!.Value.Value);
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
        Assert.Equal(1, result[0].Quantity!.Value.Value);
        Assert.Single(result[0].Sources);
    }

    [Fact]
    public void MergeManualLineIntoList_AddsItemWithoutSources()
    {
        var listId = Guid.NewGuid();
        var result = _merger.MergeManualLineIntoList(
            [],
            null,
            "Melk",
            null,
            2,
            Unit.Piece,
            listId);

        Assert.Single(result);
        Assert.Equal("Melk", result[0].DisplayName);
        Assert.Equal(2, result[0].Quantity!.Value.Value);
        Assert.Empty(result[0].Sources);
    }

    [Fact]
    public void MergeManualLineIntoList_MergesQuantityOnMatchingLine()
    {
        var listId = Guid.NewGuid();
        var existing = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = listId,
            DisplayName = "Melk",
            Quantity = new Quantity(1),
            Unit = Unit.Piece,
            Sources = [],
        };

        var result = _merger.MergeManualLineIntoList(
            [existing],
            null,
            "Melk",
            null,
            2,
            Unit.Piece,
            listId);

        Assert.Single(result);
        Assert.Equal(3, result[0].Quantity!.Value.Value);
    }

    [Fact]
    public void MergeIntoList_MergesTwoUnmeasuredLinesWithSameName()
    {
        var listId = Guid.NewGuid();
        var existing = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = listId,
            DisplayName = "olijfolie",
            Preparation = "naar smaak",
            Quantity = null,
            Unit = null,
            Sources =
            [
                new ShoppingListItemSource
                {
                    ShoppingListItemId = Guid.Empty,
                    RecipeId = Guid.NewGuid(),
                    RecipeTitle = "Bruschetta",
                },
            ],
        };
        existing.Sources.First().ShoppingListItemId = existing.Id;

        var result = _merger.MergeIntoList(
            [existing],
            [
                new ShoppingListIngredientLine
                {
                    DisplayName = "olijfolie",
                    Preparation = "naar smaak",
                    Quantity = null,
                    Unit = null,
                    RecipeId = Guid.NewGuid(),
                    RecipeTitle = "Pasta",
                },
            ],
            listId);

        Assert.Single(result);
        Assert.Null(result[0].Quantity);
        Assert.Null(result[0].Unit);
        Assert.Equal(2, result[0].Sources.Count);
    }

    [Fact]
    public void MergeIntoList_KeepsSeparateLinesWhenMeasuredAndUnmeasured()
    {
        var listId = Guid.NewGuid();
        var existing = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = listId,
            DisplayName = "zout",
            Quantity = new Quantity(1),
            Unit = Unit.Teaspoon,
            Sources = [],
        };

        var result = _merger.MergeIntoList(
            [existing],
            [
                new ShoppingListIngredientLine
                {
                    DisplayName = "zout",
                    Preparation = "naar smaak",
                    Quantity = null,
                    Unit = null,
                    RecipeId = Guid.NewGuid(),
                    RecipeTitle = "Soup",
                },
            ],
            listId);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void MergeIntoList_DoesNotDuplicateUnmeasuredSourceForSameRecipe()
    {
        var listId = Guid.NewGuid();
        var recipeId = Guid.NewGuid();
        var existing = new ShoppingListItem
        {
            Id = Guid.NewGuid(),
            ShoppingListId = listId,
            DisplayName = "peper",
            Preparation = "naar smaak",
            Quantity = null,
            Unit = null,
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
                    DisplayName = "peper",
                    Preparation = "naar smaak",
                    Quantity = null,
                    Unit = null,
                    RecipeId = recipeId,
                    RecipeTitle = "Soup",
                },
            ],
            listId);

        Assert.Single(result);
        Assert.Null(result[0].Quantity);
        Assert.Single(result[0].Sources);
    }

    [Fact]
    public void MergeManualLineIntoList_AddsUnmeasuredItem()
    {
        var listId = Guid.NewGuid();
        var result = _merger.MergeManualLineIntoList(
            [],
            null,
            "olijfolie",
            "naar smaak",
            null,
            null,
            listId);

        Assert.Single(result);
        Assert.Equal("olijfolie", result[0].DisplayName);
        Assert.Null(result[0].Quantity);
        Assert.Null(result[0].Unit);
    }
}
