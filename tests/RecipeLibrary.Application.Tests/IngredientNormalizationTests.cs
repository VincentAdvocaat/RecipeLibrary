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
}
