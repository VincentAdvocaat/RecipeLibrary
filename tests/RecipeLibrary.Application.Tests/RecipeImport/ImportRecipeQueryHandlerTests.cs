using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.RecipeImport;
using RecipeLibrary.Application.UseCases.RecipeImport;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class ImportRecipeQueryHandlerTests
{
    [Fact]
    public async Task ImportRecipeContentQueryHandler_DelegatesToImportService()
    {
        var html = await File.ReadAllTextAsync(GetFixturePath("jsonld-pasta.html"));
        var service = CreateService();
        var sut = new ImportRecipeContentQueryHandler(service);

        var result = await sut.HandleAsync(new ImportRecipeContentQuery { Content = html, ContentKind = ImportContentKind.Html });

        Assert.Equal("Snelle pasta", result.Title);
        Assert.Equal(ImportSource.PlainText, result.Source);
    }

    [Fact]
    public async Task ImportRecipeFromUrlQueryHandler_FetchesAndImports()
    {
        var html = await File.ReadAllTextAsync(GetFixturePath("jsonld-pasta.html"));
        var fetcher = new FakeContentFetcher(html);
        var sut = new ImportRecipeFromUrlQueryHandler(fetcher, new NullSocialCaptionFetcher(), CreateService());

        var result = await sut.HandleAsync(new ImportRecipeFromUrlQuery { Url = "https://example.com/recipe" });

        Assert.Equal("Snelle pasta", result.Title);
        Assert.Equal("https://example.com/recipe", fetcher.LastUrl);
        Assert.DoesNotContain(ImportWarningCodes.UrlContentTruncated, result.Warnings);
    }

    [Fact]
    public async Task ImportRecipeFromUrlQueryHandler_AddsWarning_WhenContentTruncated()
    {
        var html = await File.ReadAllTextAsync(GetFixturePath("jsonld-pasta.html"));
        var fetcher = new FakeContentFetcher(html, wasTruncated: true);
        var sut = new ImportRecipeFromUrlQueryHandler(fetcher, new NullSocialCaptionFetcher(), CreateService());

        var result = await sut.HandleAsync(new ImportRecipeFromUrlQuery { Url = "https://example.com/recipe" });

        Assert.Equal("Snelle pasta", result.Title);
        Assert.Contains(ImportWarningCodes.UrlContentTruncated, result.Warnings);
    }

    [Fact]
    public async Task ImportRecipeFromUrlQueryHandler_UsesSocialCaption_WhenAvailable()
    {
        var caption = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "YouTubePanangCurry", "clean-data.txt"));
        var htmlFetcher = new FakeContentFetcher("<html><body>should not be used</body></html>");
        var social = new FakeSocialCaptionFetcher(caption);
        var sut = new ImportRecipeFromUrlQueryHandler(htmlFetcher, social, CreateService());

        var result = await sut.HandleAsync(new ImportRecipeFromUrlQuery
        {
            Url = "https://www.youtube.com/shorts/DSGRNoSTvLg",
        });

        Assert.Equal("Panang Curry", result.Title);
        Assert.True(result.Ingredients.Count >= 5);
        Assert.True(result.Steps.Count >= 5);
        Assert.Null(htmlFetcher.LastUrl);
        Assert.Equal("https://www.youtube.com/shorts/DSGRNoSTvLg", social.LastUrl);
    }

    [Fact]
    public async Task ImportRecipeFromUrlQueryHandler_Throws_ForInvalidUrl()
    {
        var sut = new ImportRecipeFromUrlQueryHandler(
            new FakeContentFetcher(""),
            new NullSocialCaptionFetcher(),
            CreateService());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new ImportRecipeFromUrlQuery { Url = "not-a-url" }));
    }

    [Fact]
    public async Task ImportRecipeFromUrlQueryHandler_Throws_ForPrivateHost()
    {
        var sut = new ImportRecipeFromUrlQueryHandler(
            new FakeContentFetcher(""),
            new NullSocialCaptionFetcher(),
            CreateService());

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new ImportRecipeFromUrlQuery { Url = "http://127.0.0.1/recipe" }));
    }

    [Fact]
    public async Task ImportRecipeFromImageQueryHandler_ExtractsTextAndImports()
    {
        var plain = await File.ReadAllTextAsync(GetFixturePath("plain-pasta.txt"));
        var extractor = new FakeImageTextExtractor(plain);
        var sut = new ImportRecipeFromImageQueryHandler(
            extractor,
            CreateService(),
            Options.Create(new RecipeImportOptions()));

        var result = await sut.HandleAsync(new ImportRecipeFromImageQuery
        {
            ImageBytes = [1, 2, 3],
            ContentType = "image/png",
            Language = "nl",
        });

        Assert.NotNull(result);
        Assert.Equal("nld", extractor.LastLanguage);
        Assert.Equal("Snelle pasta", result.Title);
        Assert.True(result.Ingredients.Count >= 2);
        Assert.Contains(result.Ingredients, i => i.Name.Contains("pasta", StringComparison.OrdinalIgnoreCase));
        Assert.True(result.Steps.Count >= 2);
    }

    [Fact]
    public async Task ImportRecipeFromImageQueryHandler_Throws_ForEmptyImage()
    {
        var sut = new ImportRecipeFromImageQueryHandler(
            new FakeImageTextExtractor("x"),
            CreateService(),
            Options.Create(new RecipeImportOptions()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new ImportRecipeFromImageQuery
            {
                ImageBytes = [],
                ContentType = "image/png",
                Language = "eng",
            }));
    }

    [Fact]
    public async Task ImportRecipeFromImageQueryHandler_Throws_ForUnsupportedType()
    {
        var sut = new ImportRecipeFromImageQueryHandler(
            new FakeImageTextExtractor("x"),
            CreateService(),
            Options.Create(new RecipeImportOptions()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new ImportRecipeFromImageQuery
            {
                ImageBytes = [1],
                ContentType = "application/pdf",
                Language = "eng",
            }));
    }

    [Fact]
    public async Task ImportRecipeFromImageQueryHandler_Throws_ForOversizedImage()
    {
        var options = Options.Create(new RecipeImportOptions
        {
            Ocr = new RecipeImportOcrOptions { MaxImageBytes = 4 },
        });
        var sut = new ImportRecipeFromImageQueryHandler(
            new FakeImageTextExtractor("x"),
            CreateService(),
            options);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new ImportRecipeFromImageQuery
            {
                ImageBytes = [1, 2, 3, 4, 5],
                ContentType = "image/png",
                Language = "eng",
            }));
    }

    [Fact]
    public async Task ImportRecipeFromImageQueryHandler_InfersTypeFromFileName_WhenContentTypeEmpty()
    {
        var plain = await File.ReadAllTextAsync(GetFixturePath("plain-pasta.txt"));
        var extractor = new FakeImageTextExtractor(plain);
        var sut = new ImportRecipeFromImageQueryHandler(
            extractor,
            CreateService(),
            Options.Create(new RecipeImportOptions()));

        var result = await sut.HandleAsync(new ImportRecipeFromImageQuery
        {
            ImageBytes = [1, 2, 3],
            ContentType = string.Empty,
            FileName = "recipe.png",
            Language = "eng",
        });

        Assert.NotNull(result);
    }

    [Fact]
    public async Task ImportRecipeFromImageQueryHandler_Throws_WhenContentTypeAndExtensionMissing()
    {
        var sut = new ImportRecipeFromImageQueryHandler(
            new FakeImageTextExtractor("x"),
            CreateService(),
            Options.Create(new RecipeImportOptions()));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new ImportRecipeFromImageQuery
            {
                ImageBytes = [1],
                ContentType = string.Empty,
                FileName = "recipe.bin",
                Language = "eng",
            }));
    }

    [Fact]
    public async Task ImportRecipeFromImageQueryHandler_MultipleImages_ConcatenatesOcrText()
    {
        var extractor = new QueuedImageTextExtractor(
        [
            """
            Ingrediënten
            200 gr pasta
            """,
            """
            Bereiding
            1. Kook de pasta.
            """,
        ]);
        var sut = new ImportRecipeFromImageQueryHandler(
            extractor,
            CreateService(),
            Options.Create(new RecipeImportOptions { Ocr = new RecipeImportOcrOptions { MaxImagesPerImport = 5 } }));

        var result = await sut.HandleAsync(new ImportRecipeFromImageQuery
        {
            Images =
            [
                new ImportImageFile { ImageBytes = [1], ContentType = "image/png", FileName = "a.png" },
                new ImportImageFile { ImageBytes = [2], ContentType = "image/png", FileName = "b.png" },
            ],
            Language = "nl",
        });

        Assert.Equal(2, extractor.CallCount);
        Assert.True(result.Ingredients.Count > 0);
        Assert.True(result.Steps.Count > 0);
    }

    [Fact]
    public async Task ImportRecipeFromImageQueryHandler_Throws_WhenTooManyImages()
    {
        var sut = new ImportRecipeFromImageQueryHandler(
            new FakeImageTextExtractor("x"),
            CreateService(),
            Options.Create(new RecipeImportOptions { Ocr = new RecipeImportOcrOptions { MaxImagesPerImport = 1 } }));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            sut.HandleAsync(new ImportRecipeFromImageQuery
            {
                Images =
                [
                    new ImportImageFile { ImageBytes = [1], ContentType = "image/png" },
                    new ImportImageFile { ImageBytes = [2], ContentType = "image/png" },
                ],
            }));
    }

    private static RecipeImportService CreateService() => ImportTestFactory.CreateImportService();

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "recipe-import", fileName);

    private sealed class EmptyIngredientRepository : IIngredientRepository
    {
        public Task AddMatchLogAsync(Domain.Entities.IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;
        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) => Task.CompletedTask;
        public Task<Domain.Entities.CanonicalIngredient> FindOrCreateAsync(string languageCode, string displayName, string normalizedDisplayName, string? alias, string? normalizedAlias, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<Domain.Entities.CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, IReadOnlyList<string> languageCodes, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Domain.Entities.CanonicalIngredient>>([]);
        public Task<Domain.Entities.CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, IReadOnlyList<string> languageCodes, CancellationToken ct = default) => Task.FromResult<Domain.Entities.CanonicalIngredient?>(null);
        public Task<Domain.Entities.CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, IReadOnlyList<string> languageCodes, CancellationToken ct = default) => Task.FromResult<Domain.Entities.CanonicalIngredient?>(null);
        public Task<IReadOnlyList<Domain.Entities.CanonicalIngredient>> SearchAsync(string normalizedQuery, IReadOnlyList<string> languageCodes, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Domain.Entities.CanonicalIngredient>>([]);
        public Task<IReadOnlyList<Domain.Entities.Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Domain.Entities.Tag>>([]);
    }

    private sealed class FakeContentFetcher(string html, bool wasTruncated = false) : IRecipeImportContentFetcher
    {
        public string? LastUrl { get; private set; }

        public Task<RecipeImportFetchedContent> FetchHtmlAsync(string url, CancellationToken ct = default)
        {
            LastUrl = url;
            return Task.FromResult(new RecipeImportFetchedContent(html, wasTruncated));
        }
    }

    private sealed class NullSocialCaptionFetcher : IRecipeSocialCaptionFetcher
    {
        public Task<string?> TryFetchCaptionAsync(string url, CancellationToken ct = default) =>
            Task.FromResult<string?>(null);
    }

    private sealed class FakeSocialCaptionFetcher(string caption) : IRecipeSocialCaptionFetcher
    {
        public string? LastUrl { get; private set; }

        public Task<string?> TryFetchCaptionAsync(string url, CancellationToken ct = default)
        {
            LastUrl = url;
            return Task.FromResult<string?>(caption);
        }
    }

    private sealed class FakeImageTextExtractor(string text) : IRecipeImageTextExtractor
    {
        public string? LastLanguage { get; private set; }

        public Task<string> ExtractTextAsync(Stream imageStream, string language, CancellationToken ct = default)
        {
            LastLanguage = language;
            return Task.FromResult(text);
        }
    }

    private sealed class QueuedImageTextExtractor(IReadOnlyList<string> texts) : IRecipeImageTextExtractor
    {
        private int _index;

        public int CallCount { get; private set; }

        public Task<string> ExtractTextAsync(Stream imageStream, string language, CancellationToken ct = default)
        {
            CallCount++;
            var text = _index < texts.Count ? texts[_index++] : string.Empty;
            return Task.FromResult(text);
        }
    }
}
