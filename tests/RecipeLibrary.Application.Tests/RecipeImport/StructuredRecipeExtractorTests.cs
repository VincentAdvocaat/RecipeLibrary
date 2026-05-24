using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.RecipeImport;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class StructuredRecipeExtractorTests
{
    private readonly StructuredRecipeExtractor _sut = new();

    [Fact]
    public void Extract_ParsesJsonLdRecipe()
    {
        var html = File.ReadAllText(GetFixturePath("jsonld-pasta.html"));

        var result = _sut.Extract(html, ImportContentKind.Html);

        Assert.Equal(ImportSource.JsonLd, result.Source);
        Assert.Equal("Snelle pasta", result.Title);
        Assert.Equal(15, result.PreparationTimeMinutes);
        Assert.Equal(20, result.CookingTimeMinutes);
        Assert.Equal(3, result.IngredientLines.Count);
        Assert.Equal(2, result.Steps.Count);
    }

    [Fact]
    public void Extract_ParsesPlainTextSections()
    {
        var text = File.ReadAllText(GetFixturePath("plain-pasta.txt"));

        var result = _sut.Extract(text, ImportContentKind.PlainText);

        Assert.Equal(ImportSource.PlainText, result.Source);
        Assert.Equal("Snelle pasta", result.Title);
        Assert.True(result.IngredientLines.Count >= 3);
        Assert.True(result.Steps.Count >= 2);
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "recipe-import", fileName);
}
