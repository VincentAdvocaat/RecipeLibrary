using RecipeLibrary.Application.Abstractions;
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

    [Fact]
    public async Task Parse_AiFallbackFailure_EmitsDistinctWarning()
    {
        var sut = ImportTestFactory.CreateTextParser(
            new ThrowingIngredientLineAiParser(),
            ImportTestFactory.AiEnabledOptions);

        var result = await sut.ParseAsync(
            """
            Dual Measure Soup

            Ingredients
            390 gm/ 3 medium tomatoes

            Instructions
            1. Cook.
            """,
            new ImportRecipeParseOptions { UseAiFallback = true });

        Assert.Contains(ImportWarningCodes.AiFallbackFailed, result.Warnings);
        Assert.DoesNotContain(ImportWarningCodes.LowConfidenceAiHint, result.Warnings);
        Assert.Equal(ImportParseMethod.Deterministic, result.Ingredients[0].ParseMethod);
    }

    [Fact]
    public void NormalizePlainTextForAi_StripsChromeAndKeepsRecipeSections()
    {
        var noisy = """
            Home
            Save recipe
            Chickpea Curry

            Ingredients
            200 g chickpeas

            Instructions
            1. Simmer until soft.

            Serving Suggestions
            Serve with rice.

            Related
            More curries
            """;

        var normalized = RecipeTextDocumentExtractor.NormalizePlainTextForAi(noisy);

        Assert.Contains("Chickpea Curry", normalized, StringComparison.Ordinal);
        Assert.Contains("200 g chickpeas", normalized, StringComparison.Ordinal);
        Assert.Contains("Simmer until soft", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("Save recipe", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Serving Suggestions", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Related", normalized, StringComparison.Ordinal);
    }

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "recipe-import", fileName);

    private sealed class ThrowingIngredientLineAiParser : IIngredientLineAiParser
    {
        public Task<IReadOnlyList<AiParsedIngredientLine>> ParseLinesAsync(
            IReadOnlyList<string> rawLines,
            CancellationToken ct = default) =>
            throw new InvalidOperationException("Simulated AI failure.");
    }
}
