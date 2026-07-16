using System.Net;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.RecipeImport;
using Xunit;

namespace RecipeLibrary.Application.Tests.RecipeImport;

public sealed class RecipeImportUrlSafetyTests
{
    [Theory]
    [InlineData("http://127.0.0.1/recipe")]
    [InlineData("https://localhost/recipe")]
    [InlineData("http://192.168.1.10/recipe")]
    [InlineData("http://10.0.0.5/recipe")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/")]
    public async Task EnsurePublicHttpUrlAsync_BlocksPrivateOrLoopbackHosts(string url)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            RecipeImportUrlSafety.EnsurePublicHttpUrlAsync(url));
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.com/recipe")]
    [InlineData("file:///etc/passwd")]
    public async Task EnsurePublicHttpUrlAsync_BlocksNonHttpSchemes(string url)
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            RecipeImportUrlSafety.EnsurePublicHttpUrlAsync(url));
    }

    [Fact]
    public void IsBlockedAddress_FlagsPrivateRanges()
    {
        Assert.True(RecipeImportUrlSafety.IsBlockedAddress(IPAddress.Parse("127.0.0.1")));
        Assert.True(RecipeImportUrlSafety.IsBlockedAddress(IPAddress.Parse("10.1.2.3")));
        Assert.True(RecipeImportUrlSafety.IsBlockedAddress(IPAddress.Parse("172.16.0.1")));
        Assert.True(RecipeImportUrlSafety.IsBlockedAddress(IPAddress.Parse("192.168.0.1")));
        Assert.True(RecipeImportUrlSafety.IsBlockedAddress(IPAddress.Parse("169.254.169.254")));
        Assert.False(RecipeImportUrlSafety.IsBlockedAddress(IPAddress.Parse("8.8.8.8")));
        Assert.False(RecipeImportUrlSafety.IsBlockedAddress(IPAddress.Parse("1.1.1.1")));
    }
}

public sealed class RecipeImportLooksLikeHtmlTests
{
    [Theory]
    [InlineData("<!DOCTYPE html><html><body>x</body></html>", true)]
    [InlineData("<html lang=\"nl\"><body>x</body></html>", true)]
    [InlineData("<script type=\"application/ld+json\">{}</script>", true)]
    [InlineData("Snijd tot de kerntemperatuur < 65°C is.", false)]
    [InlineData("1 ui > 2 tenen knoflook", false)]
    [InlineData("Gebruik a < b of b > a in de notities.", false)]
    public void LooksLikeHtml_OnlyDetectsDocumentMarkup(string content, bool expected)
    {
        Assert.Equal(expected, RecipeImportService.LooksLikeHtml(content));
    }

    [Fact]
    public async Task ImportContent_Auto_DoesNotTreatComparisonTextAsHtml()
    {
        var service = new RecipeImportService(
            new RecipeTextParser(new IngredientLineParser(new IngredientLineResolver(new IngredientNameParser()))),
            new HtmlRecipeTextExtractor(),
            new IngredientMatcher(
                new EmptyIngredientRepository(),
                new IngredientTextNormalizer(),
                new IngredientSimilarityScorer()));

        var result = await service.ImportContentAsync(new ImportRecipeContentQuery
        {
            Content = """
                Ingrediënten: Test
                1 ui
                Bereiding: Test
                Bak tot temperatuur < 65 C.
                """,
            ContentKind = ImportContentKind.Auto,
        });

        Assert.Contains(result.Steps, s => s.Text.Contains('<', StringComparison.Ordinal));
    }

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
}
