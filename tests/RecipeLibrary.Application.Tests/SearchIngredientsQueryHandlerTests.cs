using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.Ingredients;
using RecipeLibrary.Domain.Entities;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class SearchIngredientsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_ReturnsDisplayName_NotEmpty()
    {
        var tomatoId = Guid.NewGuid();
        var repo = new CanonicalIngredientRepository(
        [
            IngredientTestFactory.Create("tomaat", id: tomatoId),
        ]);

        var sut = new SearchIngredientsQueryHandler(repo, new IngredientTextNormalizer());
        var result = await sut.HandleAsync(new SearchIngredientsQuery { Query = "toma", CultureName = "nl" });

        Assert.Single(result);
        Assert.Equal("tomaat", result[0].Name);
        Assert.Equal("nl", result[0].LanguageCode);
        Assert.Equal(tomatoId, result[0].Id);
    }

    [Fact]
    public async Task HandleAsync_FallsBackToEnglishDisplayName()
    {
        var tomato = IngredientTestFactory.Create("tomato", "en", catalogKey: "tomato");
        var repo = new CanonicalIngredientRepository([tomato]);

        var sut = new SearchIngredientsQueryHandler(repo, new IngredientTextNormalizer());
        var result = await sut.HandleAsync(new SearchIngredientsQuery { Query = "tom", CultureName = "nl-NL" });

        Assert.Single(result);
        Assert.Equal("tomato", result[0].Name);
        Assert.Equal("en", result[0].LanguageCode);
    }

    private sealed class CanonicalIngredientRepository(IReadOnlyList<CanonicalIngredient> ingredients)
        : IIngredientRepository
    {
        public Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;

        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) => Task.CompletedTask;

        public Task<CanonicalIngredient> FindOrCreateAsync(
            string languageCode,
            string displayName,
            string normalizedDisplayName,
            string? alias,
            string? normalizedAlias,
            CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(
            string normalizedQuery,
            IReadOnlyList<string> languageCodes,
            int take,
            CancellationToken ct = default)
            => Task.FromResult(ingredients);

        public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(
            string normalizedAlias,
            IReadOnlyList<string> languageCodes,
            CancellationToken ct = default)
            => Task.FromResult<CanonicalIngredient?>(null);

        public Task<CanonicalIngredient?> GetByNormalizedNameAsync(
            string normalizedName,
            IReadOnlyList<string> languageCodes,
            CancellationToken ct = default)
            => Task.FromResult(ingredients.FirstOrDefault(x =>
                x.Translations.Any(t => t.NormalizedDisplayName == normalizedName)));

        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(
            string normalizedQuery,
            IReadOnlyList<string> languageCodes,
            int take,
            CancellationToken ct = default)
        {
            var matches = ingredients
                .Where(x => x.Translations.Any(t =>
                    t.NormalizedDisplayName.Contains(normalizedQuery, StringComparison.Ordinal)))
                .Take(take)
                .ToList();
            return Task.FromResult<IReadOnlyList<CanonicalIngredient>>(matches);
        }

        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tag>>([]);
    }
}
