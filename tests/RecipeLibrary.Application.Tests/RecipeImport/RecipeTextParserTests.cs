using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.RecipeImport;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class RecipeTextParserTests
{
    private readonly RecipeTextParser _sut = ImportTestFactory.CreateTextParser();

    [Fact]
    public async Task Parse_ParsesPlainTextSections()
    {
        var text = File.ReadAllText(GetFixturePath("plain-pasta.txt"));

        var result = await _sut.ParseAsync(text, new ImportRecipeParseOptions { UseAiFallback = false });

        Assert.Equal(ImportSource.PlainText, result.Source);
        Assert.Equal("Snelle pasta", result.Title);
        Assert.True(result.Ingredients.Count >= 3);
        Assert.True(result.Steps.Count >= 2);
    }

    [Fact]
    public async Task Parse_ParsesHtmlViaHtmlExtractorThenParser()
    {
        var html = File.ReadAllText(GetFixturePath("jsonld-pasta.html"));
        var text = new HtmlRecipeTextExtractor().Extract(html);

        var result = await _sut.ParseAsync(text, new ImportRecipeParseOptions { UseAiFallback = false });

        Assert.Equal("Snelle pasta", result.Title);
        Assert.True(result.Ingredients.Count >= 3);
        Assert.Equal("pasta", result.Ingredients[0].Name);
        Assert.Equal(200, result.Ingredients[0].Quantity);
        Assert.Equal("Gram", result.Ingredients[0].Unit);
        Assert.True(result.Steps.Count >= 2);
        Assert.Equal(15, result.PreparationTimeMinutes);
        Assert.Equal(20, result.CookingTimeMinutes);
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "recipe-import", fileName);
}
