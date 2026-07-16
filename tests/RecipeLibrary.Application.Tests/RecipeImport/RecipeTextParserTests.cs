using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.RecipeImport;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class RecipeTextParserTests
{
    private readonly RecipeTextParser _sut = new(
        new IngredientLineParser(new IngredientLineResolver(new IngredientNameParser())));

    [Fact]
    public void Parse_ParsesPlainTextSections()
    {
        var text = File.ReadAllText(GetFixturePath("plain-pasta.txt"));

        var result = _sut.Parse(text);

        Assert.Equal(ImportSource.PlainText, result.Source);
        Assert.Equal("Snelle pasta", result.Title);
        Assert.True(result.Ingredients.Count >= 3);
        Assert.True(result.Steps.Count >= 2);
    }

    [Fact]
    public void Parse_ParsesHtmlViaHtmlExtractorThenParser()
    {
        var html = File.ReadAllText(GetFixturePath("jsonld-pasta.html"));
        var text = new HtmlRecipeTextExtractor().Extract(html);

        var result = _sut.Parse(text);

        Assert.Equal("Snelle pasta", result.Title);
        Assert.True(result.Ingredients.Count >= 3);
        Assert.Equal("pasta", result.Ingredients[0].Name);
        Assert.Equal(200, result.Ingredients[0].Quantity);
        Assert.Equal("Gram", result.Ingredients[0].Unit);
        Assert.True(result.Steps.Count >= 2);
        Assert.Equal(35, result.CookingTimeMinutes);
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "recipe-import", fileName);
}
