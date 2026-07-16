using Xunit;
using RecipeLibrary.Application.Ingredients;

namespace RecipeLibrary.Application.Tests;

public sealed class IngredientNormalizationTests
{
    [Fact]
    public void Normalize_RemovesDiacriticsAndWhitespace()
    {
        var sut = new IngredientTextNormalizer();

        var normalized = sut.Normalize("  Verse Gémbér  ");

        Assert.Equal("verse gember", normalized);
    }

    [Fact]
    public void ParseIngredient_ExtractsPreparationKeyword()
    {
        var sut = new IngredientNameParser();

        var parsed = sut.ParseIngredient("gember geraspt");

        Assert.Equal("gember", parsed.Name);
        Assert.Equal("geraspt", parsed.Preparation);
    }

    [Fact]
    public void ParseIngredient_ExtractsMultiWordPhrase()
    {
        var sut = new IngredientNameParser();

        var parsed = sut.ParseIngredient("ui fijn gesneden");

        Assert.Equal("ui", parsed.Name);
        Assert.Equal("fijn gesneden", parsed.Preparation);
    }

    [Fact]
    public void ParseIngredient_CommaSeparated_DoesNotLeaveTrailingCommaOnName()
    {
        var sut = new IngredientNameParser();

        var parsed = sut.ParseIngredient("ui, fijngehakt");

        Assert.Equal("ui", parsed.Name);
        Assert.Equal("fijngehakt", parsed.Preparation);
    }

    [Fact]
    public void ParseIngredient_ExtractsInBlokjes()
    {
        var sut = new IngredientNameParser();

        var parsed = sut.ParseIngredient("courgette in blokjes");

        Assert.Equal("courgette", parsed.Name);
        Assert.Equal("in blokjes", parsed.Preparation);
    }
}
