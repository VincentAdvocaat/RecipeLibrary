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
    public void Format_UsesWholeNumbers_ForNonCulinaryUnits(decimal quantity, Unit unit, string expected)
    {
        Assert.Equal(expected, IngredientQuantityFormatter.Format(quantity, unit));
    }

    [Theory]
    [InlineData(0.5, Unit.Teaspoon, "½")]
    [InlineData(1.5, Unit.Teaspoon, "1½")]
    [InlineData(0.25, Unit.Tablespoon, "¼")]
    [InlineData(2.75, Unit.Teaspoon, "2¾")]
    public void Format_UsesCulinaryFractions_ForTeaspoonAndTablespoon(decimal quantity, Unit unit, string expected)
    {
        Assert.Equal(expected, IngredientQuantityFormatter.Format(quantity, unit));
    }

    [Fact]
    public void Format_UsesThird_ForOneThirdTeaspoon()
    {
        Assert.Equal("⅓", IngredientQuantityFormatter.Format(CulinaryQuantityFractions.Third, Unit.Teaspoon));
        Assert.Equal("1½", IngredientQuantityFormatter.Format(1m + CulinaryQuantityFractions.Half, Unit.Teaspoon));
        Assert.Equal("⅔", IngredientQuantityFormatter.Format(CulinaryQuantityFractions.TwoThirds, Unit.Tablespoon));
    }

    [Theory]
    [InlineData(50.4, Unit.Gram, 50)]
    [InlineData(50.6, Unit.Gram, 51)]
    public void Normalize_RoundsToWholeNumber_ForGram(decimal input, Unit unit, decimal expected)
    {
        Assert.Equal(expected, IngredientQuantityFormatter.Normalize(input, unit));
    }

    [Theory]
    [InlineData(1.5, Unit.Tablespoon, 1.5)]
    [InlineData(1.9, Unit.Tablespoon, 2)]
    [InlineData(0.5, Unit.Teaspoon, 0.5)]
    public void Normalize_SnapsCulinaryFractions(decimal input, Unit unit, decimal expected)
    {
        Assert.Equal(expected, IngredientQuantityFormatter.Normalize(input, unit));
    }

    [Fact]
    public void Normalize_SnapsApproximateThird()
    {
        Assert.Equal(
            CulinaryQuantityFractions.Third,
            IngredientQuantityFormatter.Normalize(0.333m, Unit.Teaspoon));
    }

    [Fact]
    public void Normalize_SumsThreeThirdsToOne()
    {
        var sum = CulinaryQuantityFractions.Third
            + CulinaryQuantityFractions.Third
            + CulinaryQuantityFractions.Third;

        Assert.Equal(1m, IngredientQuantityFormatter.Normalize(sum, Unit.Teaspoon));
    }

    [Fact]
    public void ValidateQuantity_Throws_WhenQuantityHasFraction_ForGram()
    {
        Assert.Throws<ArgumentException>(() =>
            IngredientQuantityFormatter.ValidateQuantity(1.5m, Unit.Gram));
    }

    [Fact]
    public void ValidateQuantity_AllowsCulinaryFraction_ForTeaspoon()
    {
        IngredientQuantityFormatter.ValidateQuantity(1.5m, Unit.Teaspoon);
        IngredientQuantityFormatter.ValidateQuantity(CulinaryQuantityFractions.Third, Unit.Teaspoon);
    }

    [Fact]
    public void ValidateQuantity_Throws_WhenFarFromCulinaryFraction()
    {
        Assert.Throws<ArgumentException>(() =>
            IngredientQuantityFormatter.ValidateQuantity(1.37m, Unit.Teaspoon));
    }
}
