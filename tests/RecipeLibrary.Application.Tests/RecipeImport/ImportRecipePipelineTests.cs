using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.RecipeImport;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class ImportRecipePipelineTests
{
    [Fact]
    public async Task ImportContentAsync_ParsesJsonLdHtmlEndToEnd()
    {
        var html = await File.ReadAllTextAsync(GetFixturePath("jsonld-pasta.html"));
        var sut = CreateService();

        var result = await sut.ImportContentAsync(
            new ImportRecipeContentQuery { Content = html, ContentKind = ImportContentKind.Html });

        Assert.Equal(ImportSource.JsonLd, result.Source);
        Assert.Equal("Snelle pasta", result.Title);
        Assert.True(result.Ingredients.Count >= 3);
        Assert.Equal("pasta", result.Ingredients[0].Name);
        Assert.Equal(200, result.Ingredients[0].Quantity);
        Assert.Equal("Gram", result.Ingredients[0].Unit);
    }

    private static RecipeImportService CreateService() =>
        new(
            new StructuredRecipeExtractor(),
            new IngredientLineParser(new IngredientLineResolver(new IngredientNameParser())),
            new IngredientMatcher(new FakeIngredientRepository(), new IngredientTextNormalizer(), new IngredientSimilarityScorer()),
            new TestNullIngredientLineAiParser(),
            Options.Create(new RecipeImportOptions()));

    private static string GetFixturePath(string fileName) =>
        Path.Combine(AppContext.BaseDirectory, "Fixtures", "recipe-import", fileName);

    private sealed class TestNullIngredientLineAiParser : IIngredientLineAiParser
    {
        public Task<IReadOnlyList<AiParsedIngredientLine>> ParseLinesAsync(
            IReadOnlyList<string> rawLines,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<AiParsedIngredientLine>>([]);
    }

    private sealed class FakeIngredientRepository : IIngredientRepository
    {
        public Task<Domain.Entities.CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default) =>
            Task.FromResult<Domain.Entities.CanonicalIngredient?>(null);

        public Task<Domain.Entities.CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default) =>
            Task.FromResult<Domain.Entities.CanonicalIngredient?>(null);

        public Task<IReadOnlyList<Domain.Entities.CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Domain.Entities.CanonicalIngredient>>([]);

        public Task<IReadOnlyList<Domain.Entities.CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Domain.Entities.CanonicalIngredient>>([]);

        public Task<Domain.Entities.CanonicalIngredient> CreateIngredientWithAliasAsync(
            string canonicalName,
            string normalizedName,
            string alias,
            string normalizedAlias,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task AddMatchLogAsync(Domain.Entities.IngredientMatchLog log, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<Domain.Entities.Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Domain.Entities.Tag>>([]);

        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
