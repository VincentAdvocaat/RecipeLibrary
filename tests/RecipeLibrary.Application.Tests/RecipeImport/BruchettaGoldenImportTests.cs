using System.Text.Json;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.RecipeImport;
using RecipeLibrary.Application.UseCases.RecipeImport;
using RecipeLibrary.Application.Validators;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

/// <summary>
/// Golden import coverage for all modalities using the Bruschetta fixture set.
/// Parser heuristics stay generic; fixtures only supply realistic input shapes.
/// </summary>
public sealed class BruchettaGoldenImportTests
{
    private readonly RecipeTextParser _parser = new(
        new IngredientLineParser(new IngredientLineResolver(new IngredientNameParser())));

    private readonly HtmlRecipeTextExtractor _htmlExtractor = new();

    [Fact]
    public void Parse_CleanData_MatchesExpectedOutput_AndPassesValidator()
    {
        var actual = ParseToCommand(ReadFixture("clean-data.txt"));
        AssertMatchesExpectedCore(actual, requireExactTitle: true, expectedServings: 0);
        CreateRecipeCommandValidator.ValidateAndThrow(actual);
        Assert.Equal((int)Difficulty.Easy, actual.Difficulty);
    }

    [Fact]
    public void Parse_EntireWebPageText_MatchesExpectedCore()
    {
        var actual = ParseToCommand(ReadFixture("entire-web-page.txt"));
        AssertMatchesExpectedCore(actual, requireExactTitle: true, expectedServings: 0);
        CreateRecipeCommandValidator.ValidateAndThrow(actual);
    }

    [Fact]
    public async Task ImportContent_PlainTextCleanData_MatchesExpectedCore()
    {
        var service = CreateImportService();
        var result = await service.ImportContentAsync(new ImportRecipeContentQuery
        {
            Content = ReadFixture("clean-data.txt"),
            ContentKind = ImportContentKind.PlainText,
        });

        var actual = ImportRecipeResultMapper.ToCreateRecipeCommand(result);
        AssertMatchesExpectedCore(actual, requireExactTitle: true, expectedServings: 0);
    }

    [Fact]
    public async Task ImportContent_EntireWebPageAsPlainText_MatchesExpectedCore()
    {
        var service = CreateImportService();
        var result = await service.ImportContentAsync(new ImportRecipeContentQuery
        {
            Content = ReadFixture("entire-web-page.txt"),
            ContentKind = ImportContentKind.PlainText,
        });

        var actual = ImportRecipeResultMapper.ToCreateRecipeCommand(result);
        AssertMatchesExpectedCore(actual, requireExactTitle: true, expectedServings: 0);
    }

    [Fact]
    public async Task ImportContent_JsonLdHtml_MatchesExpectedCore_WithSchemaTitle()
    {
        var service = CreateImportService();
        var result = await service.ImportContentAsync(new ImportRecipeContentQuery
        {
            Content = ReadFixture("page-jsonld.html"),
            ContentKind = ImportContentKind.Html,
        });

        var actual = ImportRecipeResultMapper.ToCreateRecipeCommand(result);
        // JSON-LD name is the page SEO title; ingredients/steps still match the golden recipe.
        AssertMatchesExpectedCore(
            actual,
            requireExactTitle: false,
            expectedServings: 8,
            expectedPreparationTimeMinutes: 30,
            expectedCookingTimeMinutes: 0);
        Assert.False(string.IsNullOrWhiteSpace(actual.Title));
        Assert.Equal((int)Difficulty.Easy, actual.Difficulty);
    }

    [Fact]
    public async Task ImportFromUrl_UsesFetchedHtml_AndMatchesExpectedCore()
    {
        var html = ReadFixture("page-jsonld.html");
        var url = ReadFixture("url.txt").Trim();
        var fetcher = new FakeContentFetcher(html);
        var sut = new ImportRecipeFromUrlQueryHandler(fetcher, CreateImportService());

        var result = await sut.HandleAsync(new ImportRecipeFromUrlQuery { Url = url });
        var actual = ImportRecipeResultMapper.ToCreateRecipeCommand(result);

        Assert.Equal(url, fetcher.LastUrl);
        AssertMatchesExpectedCore(
            actual,
            requireExactTitle: false,
            expectedServings: 8,
            expectedPreparationTimeMinutes: 30,
            expectedCookingTimeMinutes: 0);
        Assert.Equal((int)Difficulty.Easy, actual.Difficulty);
    }

    [Fact]
    public async Task ImportFromImage_OcrTextFixture_MatchesExpectedCore()
    {
        var ocrText = ReadFixture("ocr-text.txt");
        var extractor = new FakeImageTextExtractor(ocrText);
        var sut = new ImportRecipeFromImageQueryHandler(
            extractor,
            CreateImportService(),
            Options.Create(new RecipeImportOptions()));

        var screenshotBytes = ReadFixtureBytes(Path.Combine("screenshots", "Screenshot 2026-07-16 163539.png"));
        var result = await sut.HandleAsync(new ImportRecipeFromImageQuery
        {
            ImageBytes = screenshotBytes,
            ContentType = "image/png",
            FileName = "screenshot.png",
            Language = "nl",
        });

        Assert.Equal("nld", extractor.LastLanguage);
        Assert.True(extractor.LastStreamLength > 0);

        var actual = ImportRecipeResultMapper.ToCreateRecipeCommand(result);
        AssertMatchesExpectedCore(actual, requireExactTitle: true, expectedServings: 0);
    }

    [Fact]
    public void HtmlExtractor_JsonLdHowToSection_ProducesInstructionSteps()
    {
        var text = _htmlExtractor.Extract(ReadFixture("page-jsonld.html"));
        var parsed = _parser.Parse(text);

        Assert.Equal(6, parsed.Steps.Count);
        Assert.StartsWith("Vul een kom", parsed.Steps[0].Text, StringComparison.Ordinal);
        Assert.DoesNotContain(parsed.Steps, s => s.Text.Equals("Bereiding", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Screenshots_ArePresent_ForManualOcrExploration()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Bruchetta", "screenshots");
        Assert.True(Directory.Exists(dir));
        var files = Directory.GetFiles(dir, "*.png");
        Assert.True(files.Length >= 4, $"Expected at least 4 screenshots, found {files.Length}");
    }

    private CreateRecipeCommand ParseToCommand(string plainText) =>
        ImportRecipeResultMapper.ToCreateRecipeCommand(_parser.Parse(plainText));

    private void AssertMatchesExpectedCore(
        CreateRecipeCommand actual,
        bool requireExactTitle,
        int expectedServings,
        int? expectedPreparationTimeMinutes = null,
        int? expectedCookingTimeMinutes = null)
    {
        var expected = LoadExpected();

        if (requireExactTitle)
        {
            Assert.Equal(expected.Title, actual.Title);
        }

        Assert.Equal(expected.Description, actual.Description);
        Assert.Equal(
            expectedPreparationTimeMinutes ?? expected.PreparationTimeMinutes,
            actual.PreparationTimeMinutes);
        Assert.Equal(
            expectedCookingTimeMinutes ?? expected.CookingTimeMinutes,
            actual.CookingTimeMinutes);
        Assert.Equal(expected.Difficulty, actual.Difficulty);
        Assert.Equal(expected.Category, actual.Category);
        Assert.Equal(expectedServings, actual.Servings);
        Assert.Equal(expected.Ingredients.Count, actual.Ingredients.Count);

        for (var i = 0; i < expected.Ingredients.Count; i++)
        {
            Assert.Equal(expected.Ingredients[i].Name, actual.Ingredients[i].Name);
            Assert.Equal(expected.Ingredients[i].Preparation, actual.Ingredients[i].Preparation);
            Assert.Equal(expected.Ingredients[i].Quantity, actual.Ingredients[i].Quantity);
            Assert.Equal(expected.Ingredients[i].Unit, actual.Ingredients[i].Unit);
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

    private static RecipeImportService CreateImportService() =>
        new(
            new RecipeTextParser(new IngredientLineParser(new IngredientLineResolver(new IngredientNameParser()))),
            new HtmlRecipeTextExtractor(),
            new IngredientMatcher(new EmptyIngredientRepository(), new IngredientTextNormalizer(), new IngredientSimilarityScorer()));

    private static string ReadFixture(string relativePath) =>
        File.ReadAllText(GetFixturePath(relativePath));

    private static byte[] ReadFixtureBytes(string relativePath) =>
        File.ReadAllBytes(GetFixturePath(relativePath));

    private static string GetFixturePath(string relativePath) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "Bruchetta", relativePath);

    private sealed class EmptyIngredientRepository : IIngredientRepository
    {
        public Task AddMatchLogAsync(Domain.Entities.IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Domain.Entities.CanonicalIngredient> CreateIngredientWithAliasAsync(string canonicalName, string normalizedName, string alias, string normalizedAlias, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Domain.Entities.CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Domain.Entities.CanonicalIngredient>>([]);
        public Task<Domain.Entities.CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default) => Task.FromResult<Domain.Entities.CanonicalIngredient?>(null);
        public Task<Domain.Entities.CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default) => Task.FromResult<Domain.Entities.CanonicalIngredient?>(null);
        public Task<IReadOnlyList<Domain.Entities.CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Domain.Entities.CanonicalIngredient>>([]);
        public Task<IReadOnlyList<Domain.Entities.Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Domain.Entities.Tag>>([]);
    }

    private sealed class FakeContentFetcher(string html) : IRecipeImportContentFetcher
    {
        public string? LastUrl { get; private set; }

        public Task<string> FetchHtmlAsync(string url, CancellationToken ct = default)
        {
            LastUrl = url;
            return Task.FromResult(html);
        }
    }

    private sealed class FakeImageTextExtractor(string text) : IRecipeImageTextExtractor
    {
        public string? LastLanguage { get; private set; }

        public long LastStreamLength { get; private set; }

        public async Task<string> ExtractTextAsync(Stream imageStream, string language, CancellationToken ct = default)
        {
            LastLanguage = language;
            using var ms = new MemoryStream();
            await imageStream.CopyToAsync(ms, ct);
            LastStreamLength = ms.Length;
            return text;
        }
    }
}
