using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class IngredientLineParserTests
{
    private readonly IngredientLineParser _sut = new(new IngredientLineResolver(new IngredientNameParser()));

    [Fact]
    public void Parse_ParsesQuantityUnitAndName()
    {
        var result = _sut.Parse("200 gr pasta");

        Assert.Equal(200, result.Quantity);
        Assert.Equal(nameof(Unit.Gram), result.Unit);
        Assert.Equal("pasta", result.Name);
        Assert.True(result.Confidence >= 0.9m);
    }

    [Fact]
    public void Parse_ParsesCommaSeparatedPreparation()
    {
        var result = _sut.Parse("1 ui, fijngehakt");

        Assert.Equal(1, result.Quantity);
        Assert.Equal(nameof(Unit.Piece), result.Unit);
        Assert.Equal("ui", result.Name);
        Assert.Equal("fijngehakt", result.Preparation);
    }

    [Fact]
    public void Parse_ParsesVagueQuantityAsTeaspoon()
    {
        var result = _sut.Parse("Snufje peper en zout");

        Assert.Equal(1, result.Quantity);
        Assert.Equal(nameof(Unit.Teaspoon), result.Unit);
        Assert.Contains("peper", result.Name, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.Confidence < 0.7m);
    }

    [Fact]
    public void Parse_DualMeasureLine_HasLowConfidence()
    {
        var result = _sut.Parse("390 gm/ 3 medium tomatoes");

        Assert.True(result.Confidence < 0.7m);
    }

    [Fact]
    public void Parse_CanWithParentheses_HasLowConfidence()
    {
        var result = _sut.Parse("1 large can (400 g) chickpeas, drained");

        Assert.True(result.Confidence < 0.7m);
    }

    [Fact]
    public void Parse_PecanWithParentheses_DoesNotTriggerCanAmbiguity()
    {
        var result = _sut.Parse("100 g pecan (chopped)");

        Assert.True(result.Confidence >= 0.7m);
    }

    [Fact]
    public void Parse_ParsesFractionTeaspoon()
    {
        var result = _sut.Parse("1/2 tl zout");

        Assert.Equal(0.5m, result.Quantity);
        Assert.Equal(nameof(Unit.Teaspoon), result.Unit);
        Assert.Equal("zout", result.Name);
    }

    [Fact]
    public void Parse_ParsesMixedNumberWithSpace()
    {
        var result = _sut.Parse("1 1/2 tl suiker");

        Assert.Equal(1.5m, result.Quantity);
        Assert.Equal(nameof(Unit.Teaspoon), result.Unit);
        Assert.Equal("suiker", result.Name);
    }

    [Fact]
    public void Parse_ParsesUnicodeMixedTeaspoon()
    {
        var result = _sut.Parse("1½ tl zout");

        Assert.Equal(1.5m, result.Quantity);
        Assert.Equal(nameof(Unit.Teaspoon), result.Unit);
        Assert.Equal("zout", result.Name);
    }

    [Fact]
    public void Parse_ParsesPlusMixedNumber()
    {
        var result = _sut.Parse("1+1/2 el boter");

        Assert.Equal(1.5m, result.Quantity);
        Assert.Equal(nameof(Unit.Tablespoon), result.Unit);
        Assert.Equal("boter", result.Name);
    }

    [Fact]
    public void Parse_ParsesOneThirdTeaspoon()
    {
        var result = _sut.Parse("1/3 tl zout");

        Assert.Equal(CulinaryQuantityFractions.Third, result.Quantity);
        Assert.Equal(nameof(Unit.Teaspoon), result.Unit);
        Assert.Equal("zout", result.Name);
    }

    [Fact]
    public void Parse_ParsesUnicodeOneThird()
    {
        var result = _sut.Parse("⅓ tl peper");

        Assert.Equal(CulinaryQuantityFractions.Third, result.Quantity);
        Assert.Equal(nameof(Unit.Teaspoon), result.Unit);
        Assert.Equal("peper", result.Name);
    }

    [Fact]
    public void Parse_DoesNotSilentlySnapNonCulinaryFraction_ForTeaspoon()
    {
        var result = _sut.Parse("1/7 tl zout");

        Assert.Equal(1m / 7m, result.Quantity);
        Assert.Equal(nameof(Unit.Teaspoon), result.Unit);
        Assert.Equal("zout", result.Name);
    }

    [Fact]
    public void Parse_ParsesRangeIntoPreparation()
    {
        var result = _sut.Parse("2-3 wortels");

        Assert.Equal(2, result.Quantity);
        Assert.Equal("wortels", result.Name);
        Assert.Contains("2-3", result.Preparation, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ParsesToTasteLine()
    {
        var result = _sut.Parse("zout naar smaak");

        Assert.Null(result.Quantity);
        Assert.Null(result.Unit);
        Assert.Equal("zout", result.Name);
        Assert.Equal("naar smaak", result.Preparation);
    }

    [Fact]
    public void Parse_MeasuredLineWithNaarSmaak_KeepsQuantityAndUnit()
    {
        var result = _sut.Parse("1 el olie naar smaak");

        Assert.Equal(1, result.Quantity);
        Assert.Equal(nameof(Unit.Tablespoon), result.Unit);
        Assert.Equal("olie", result.Name);
        Assert.Equal("naar smaak", result.Preparation);
    }

    [Fact]
    public void Parse_VagueQuantityWithNaarSmaak_KeepsVagueMeasure()
    {
        var result = _sut.Parse("snufje peper naar smaak");

        Assert.Equal(1, result.Quantity);
        Assert.Equal(nameof(Unit.Teaspoon), result.Unit);
        Assert.Contains("peper", result.Name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ParsesListIndexBeforeQuantityAndSliceUnit()
    {
        var result = _sut.Parse("1 8 sneetjes stokbrood");

        Assert.Equal(8, result.Quantity);
        Assert.Equal(nameof(Unit.Slice), result.Unit);
        Assert.Equal("stokbrood", result.Name);
        Assert.Null(result.Preparation);
    }

    [Fact]
    public void Parse_ParsesCloveUnit()
    {
        var result = _sut.Parse("1 teen knoflook");

        Assert.Equal(1, result.Quantity);
        Assert.Equal(nameof(Unit.Clove), result.Unit);
        Assert.Equal("knoflook", result.Name);
        Assert.Null(result.Preparation);
    }

    [Fact]
    public void Parse_ParsesHandfulWithFreshPrep()
    {
        var result = _sut.Parse("Handje verse basilicum");

        Assert.Equal(1, result.Quantity);
        Assert.Equal(nameof(Unit.Handful), result.Unit);
        Assert.Equal("basilicum", result.Name);
        Assert.Equal("vers", result.Preparation);
    }

    [Fact]
    public void Parse_ParsesBareIngredientAsUnmeasuredWithoutInventingToTaste()
    {
        var result = _sut.Parse("olijfolie");

        Assert.Null(result.Quantity);
        Assert.Null(result.Unit);
        Assert.Equal("olijfolie", result.Name);
        Assert.Null(result.Preparation);
    }

    [Fact]
    public void Parse_ConvertsKilogramToGram()
    {
        var result = _sut.Parse("0,5 kg aardappelen");

        Assert.Equal(500, result.Quantity);
        Assert.Equal(nameof(Unit.Gram), result.Unit);
        Assert.Equal("aardappelen", result.Name);
    }

    [Fact]
    public void Parse_KeepsCupAsCup_NotMilliliter()
    {
        var result = _sut.Parse("1 cup basmati rice");

        Assert.Equal(1, result.Quantity);
        Assert.Equal(nameof(Unit.Cup), result.Unit);
        Assert.Equal("basmati rice", result.Name);
    }

    [Fact]
    public void Parse_ParsesGluedOunceUnit()
    {
        var result = _sut.Parse("14oz coconut milk");

        Assert.Equal(14, result.Quantity);
        Assert.Equal(nameof(Unit.Ounce), result.Unit);
        Assert.Equal("coconut milk", result.Name);
    }

    [Fact]
    public void Parse_ParsesPoundWithDecimalQuantity()
    {
        var result = _sut.Parse("1.3 lbs chicken thigh(sliced)");

        Assert.Equal(1.3m, result.Quantity);
        Assert.Equal(nameof(Unit.Pound), result.Unit);
        Assert.Equal("chicken thigh", result.Name);
        Assert.Equal("sliced", result.Preparation);
    }

    [Fact]
    public void Parse_KeepsQuarterPieceAsCulinaryFraction()
    {
        var result = _sut.Parse("1/4 avocado, diced");

        Assert.Equal(0.25m, result.Quantity);
        Assert.Equal(nameof(Unit.Piece), result.Unit);
        Assert.Equal("avocado", result.Name);
        Assert.Equal("diced", result.Preparation);
    }

    [Fact]
    public void Parse_ParsesJuiceOfPhrase()
    {
        var result = _sut.Parse("Juice of 1 lime");

        Assert.Equal(1, result.Quantity);
        Assert.Equal(nameof(Unit.Piece), result.Unit);
        Assert.Equal("lime", result.Name);
        Assert.Equal("juice", result.Preparation);
    }

    [Fact]
    public void Parse_ParsesCanUnit()
    {
        var result = _sut.Parse("1 can chickpeas");

        Assert.Equal(1, result.Quantity);
        Assert.Equal(nameof(Unit.Can), result.Unit);
        Assert.Equal("chickpeas", result.Name);
    }
}
