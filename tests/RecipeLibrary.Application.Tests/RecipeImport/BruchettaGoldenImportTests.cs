using System.Text.Json;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.RecipeImport;
using RecipeLibrary.Application.UseCases.RecipeImport;
using RecipeLibrary.Application.Validators;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

/// <summary>
/// Golden import coverage for all modalities using the Bruschetta fixture set.
/// Every modality must produce a CreateRecipeCommand that matches expected-output.json exactly.
/// </summary>
public sealed class BruchettaGoldenImportTests
{
    private static readonly ImportRecipeParseOptions GoldenParseOptions = new() { UseAiFallback = false };

    private readonly RecipeTextParser _parser = ImportTestFactory.CreateTextParser();

    [Fact]
    public async Task Parse_CleanData_MatchesExpectedOutput_AndPassesValidator()
    {
        var actual = await ParseToCommandAsync(ReadFixture("clean-data.txt"));
        AssertMatchesExpected(actual);
        CreateRecipeCommandValidator.ValidateAndThrow(actual);
    }

    [Fact]
    public async Task Parse_EntireWebPageText_MatchesExpectedOutput_AndPassesValidator()
    {
        var actual = await ParseToCommandAsync(ReadFixture("entire-web-page.txt"));
        AssertMatchesExpected(actual);
        CreateRecipeCommandValidator.ValidateAndThrow(actual);
    }

    [Fact]
    public async Task ImportContent_PlainTextCleanData_MatchesExpectedOutput()
    {
        var service = CreateImportService();
        var result = await service.ImportContentAsync(new ImportRecipeContentQuery
        {
            Content = ReadFixture("clean-data.txt"),
            ContentKind = ImportContentKind.PlainText,
            ParseOptions = GoldenParseOptions,
        });

        AssertMatchesExpected(ImportRecipeResultMapper.ToCreateRecipeCommand(result));
    }

    [Fact]
    public async Task ImportContent_EntireWebPageAsPlainText_MatchesExpectedOutput()
    {
        var service = CreateImportService();
        var result = await service.ImportContentAsync(new ImportRecipeContentQuery
        {
            Content = ReadFixture("entire-web-page.txt"),
            ContentKind = ImportContentKind.PlainText,
            ParseOptions = GoldenParseOptions,
        });

        AssertMatchesExpected(ImportRecipeResultMapper.ToCreateRecipeCommand(result));
    }

    [Fact]
    public async Task ImportContent_JsonLdHtml_MatchesExpectedOutput()
    {
        var service = CreateImportService();
        var result = await service.ImportContentAsync(new ImportRecipeContentQuery
        {
            Content = ReadFixture("page-jsonld.html"),
            ContentKind = ImportContentKind.Html,
            ParseOptions = GoldenParseOptions,
        });

        AssertMatchesExpected(ImportRecipeResultMapper.ToCreateRecipeCommand(result));
    }

    [Fact]
    public async Task ImportFromUrl_UsesFetchedHtml_AndMatchesExpectedOutput()
    {
        var html = ReadFixture("page-jsonld.html");
        var url = ReadFixture("url.txt").Trim();
        var fetcher = new FakeContentFetcher(html);
        var sut = new ImportRecipeFromUrlQueryHandler(fetcher, new NullSocialCaptionFetcher(), CreateImportService());

        var result = await sut.HandleAsync(new ImportRecipeFromUrlQuery
        {
            Url = url,
            ParseOptions = GoldenParseOptions,
        });

        Assert.Equal(url, fetcher.LastUrl);
        AssertMatchesExpected(ImportRecipeResultMapper.ToCreateRecipeCommand(result));
    }

    [Fact]
    public async Task HtmlExtractor_JsonLd_MatchesExpectedOutput()
    {
        var text = new HtmlRecipeTextExtractor().Extract(ReadFixture("page-jsonld.html"));
        AssertMatchesExpected(await ParseToCommandAsync(text));
    }

    private async Task<CreateRecipeCommand> ParseToCommandAsync(string plainText) =>
        ImportRecipeResultMapper.ToCreateRecipeCommand(
            await _parser.ParseAsync(plainText, GoldenParseOptions));

    private static void AssertMatchesExpected(CreateRecipeCommand actual)
    {
        var expected = LoadExpected();

        Assert.Equal(expected.Title, actual.Title);
        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(expected.PreparationTimeMinutes, actual.PreparationTimeMinutes);
        Assert.Equal(expected.CookingTimeMinutes, actual.CookingTimeMinutes);
        Assert.Equal(expected.Difficulty, actual.Difficulty);
        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expected.Servings, actual.Servings);
        Assert.Equal(expected.ImageUrl, actual.ImageUrl);

        Assert.Equal(expected.Ingredients.Count, actual.Ingredients.Count);
        for (var i = 0; i < expected.Ingredients.Count; i++)
        {
            Assert.Equal(expected.Ingredients[i].Name, actual.Ingredients[i].Name);
            Assert.Equal(expected.Ingredients[i].Preparation, actual.Ingredients[i].Preparation);
            Assert.Equal(expected.Ingredients[i].Quantity, actual.Ingredients[i].Quantity);
            Assert.Equal(expected.Ingredients[i].Unit, actual.Ingredients[i].Unit);
            Assert.Equal(
                expected.Ingredients[i].CreateAsNewIngredient,
                actual.Ingredients[i].CreateAsNewIngredient);
        }

        Assert.Equal(expected.InstructionSteps.Count, actual.InstructionSteps.Count);
        for (var i = 0; i < expected.InstructionSteps.Count; i++)
        {
            Assert.Equal(expected.InstructionSteps[i].StepNumber, actual.InstructionSteps[i].StepNumber);
            Assert.Equal(expected.InstructionSteps[i].Text, actual.InstructionSteps[i].Text);
        }
    }

    private static CreateRecipeCommand LoadExpected()
    {
        var expectedJson = ReadFixture("expected-output.json");
        return JsonSerializer.Deserialize<CreateRecipeCommand>(
                   expectedJson,
                   new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? throw new InvalidOperationException("Failed to deserialize expected-output.json");
    }

    private static RecipeImportService CreateImportService() => ImportTestFactory.CreateImportService();

    private static string ReadFixture(string relativePath) =>
        File.ReadAllText(GetFixturePath(relativePath));

    private static string GetFixturePath(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Bruchetta", relativePath);

    private sealed class FakeContentFetcher(string html) : IRecipeImportContentFetcher
    {
        public string? LastUrl { get; private set; }

        public Task<string> FetchHtmlAsync(string url, CancellationToken ct = default)
        {
            LastUrl = url;
            return Task.FromResult(html);
        }
    }

    private sealed class NullSocialCaptionFetcher : IRecipeSocialCaptionFetcher
    {
        public Task<string?> TryFetchCaptionAsync(string url, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);
    }
}
