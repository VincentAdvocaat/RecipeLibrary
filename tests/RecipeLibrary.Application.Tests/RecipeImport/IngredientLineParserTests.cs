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
    public void Parse_ParsesFractionTeaspoon()
    {
        var result = _sut.Parse("1/2 tl zout");

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

        Assert.Equal(1, result.Quantity);
        Assert.Equal(nameof(Unit.Piece), result.Unit);
        Assert.Equal("zout", result.Name);
        Assert.Equal("naar smaak", result.Preparation);
    }

    [Fact]
    public void Parse_ConvertsKilogramToGram()
    {
        var result = _sut.Parse("0,5 kg aardappelen");

        Assert.Equal(500, result.Quantity);
        Assert.Equal(nameof(Unit.Gram), result.Unit);
        Assert.Equal("aardappelen", result.Name);
    }
}
