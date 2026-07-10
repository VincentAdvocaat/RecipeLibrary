using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class PantrySubtractorTests
{
    private readonly PantrySubtractor _subtractor =
        new(new PantryIngredientMerger(new IngredientTextNormalizer()));

    [Fact]
    public void SubtractFromLines_ReducesQuantity_WhenPantryHasStock()
    {
        var canonicalId = Guid.NewGuid();
        var pantry = new[]
        {
            new PantryItem
            {
                Id = Guid.NewGuid(),
                DisplayName = "Melk",
                CanonicalIngredientId = canonicalId,
                Quantity = new Quantity(200),
                Unit = Unit.Gram,
            },
        };

        var lines = new[]
        {
            new ShoppingListIngredientLine
            {
                CanonicalIngredientId = canonicalId,
                DisplayName = "Melk",
                Quantity = 500,
                Unit = Unit.Gram,
                RecipeId = Guid.NewGuid(),
                RecipeTitle = "Test",
            },
        };

        var result = _subtractor.SubtractFromLines(lines, pantry);

        Assert.Single(result);
        Assert.Equal(300, result[0].Quantity);
    }

    [Fact]
    public void SubtractFromLines_OmitsLine_WhenPantryCoversFullAmount()
    {
        var pantry = new[]
        {
            new PantryItem
            {
                Id = Guid.NewGuid(),
                DisplayName = "Melk",
                Quantity = new Quantity(500),
                Unit = Unit.Gram,
            },
        };

        var lines = new[]
        {
            new ShoppingListIngredientLine
            {
                DisplayName = "Melk",
                Quantity = 500,
                Unit = Unit.Gram,
                RecipeId = Guid.NewGuid(),
                RecipeTitle = "Test",
            },
        };

        var result = _subtractor.SubtractFromLines(lines, pantry);

        Assert.Empty(result);
    }
}
