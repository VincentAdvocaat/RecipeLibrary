using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class IngredientQuantityFormatterTests
{
    [Theory]
    [InlineData(50, Unit.Gram, "50")]
    [InlineData(500, Unit.Milliliter, "500")]
    [InlineData(1, Unit.Piece, "1")]
    [InlineData(2, Unit.Tablespoon, "2")]
    public void Format_UsesWholeNumbersOnly(decimal quantity, Unit unit, string expected)
    {
        Assert.Equal(expected, IngredientQuantityFormatter.Format(quantity, unit));
    }

    [Theory]
    [InlineData(50.4, Unit.Gram, 50)]
    [InlineData(50.6, Unit.Gram, 51)]
    [InlineData(1.9, Unit.Tablespoon, 2)]
    public void Normalize_RoundsToWholeNumber(decimal input, Unit unit, decimal expected)
    {
        Assert.Equal(expected, IngredientQuantityFormatter.Normalize(input, unit));
    }

    [Fact]
    public void ValidateQuantity_Throws_WhenQuantityHasFraction()
    {
        Assert.Throws<ArgumentException>(() =>
            IngredientQuantityFormatter.ValidateQuantity(1.5m, Unit.Gram));
    }
}
