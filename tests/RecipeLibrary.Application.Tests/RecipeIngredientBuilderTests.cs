using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using Xunit;

namespace RecipeLibrary.Application.Tests;

public sealed class RecipeIngredientBuilderTests
{
    [Fact]
    public async Task BuildAsync_StoresCleanNameAndPreparation()
    {
        var recipeId = Guid.NewGuid();
        var canonicalId = Guid.NewGuid();
        var canonical = new CanonicalIngredient
        {
            Id = canonicalId,
            CanonicalName = "Wortel",
            NormalizedName = "wortel",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var repo = new FakeIngredientRepository([canonical], new Dictionary<string, string>());
        var sut = new IngredientLineResolver(new IngredientNameParser());

        var ingredients = await RecipeIngredientBuilder.BuildAsync(
            recipeId,
            [
                new CreateRecipeIngredientDto
                {
                    Name = "Wortel",
                    Preparation = "in blokjes",
                    Quantity = 2,
                    Unit = nameof(Unit.Piece),
                },
            ],
            repo,
            new IngredientTextNormalizer(),
            new IngredientMatcher(repo, new IngredientTextNormalizer()),
            sut,
            CancellationToken.None);

        Assert.Single(ingredients);
        Assert.Equal("Wortel", ingredients[0].Name);
        Assert.Equal("in blokjes", ingredients[0].Preparation);
        Assert.Equal(canonicalId, ingredients[0].IngredientId);
    }

    [Fact]
    public async Task BuildAsync_CreateAsNewIngredient_SkipsFuzzyMatch()
    {
        var recipeId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        var existing = new CanonicalIngredient
        {
            Id = existingId,
            CanonicalName = "gember",
            NormalizedName = "gember",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        var repo = new TrackingIngredientRepository([existing]);
        var sut = new IngredientLineResolver(new IngredientNameParser());

        var ingredients = await RecipeIngredientBuilder.BuildAsync(
            recipeId,
            [
                new CreateRecipeIngredientDto
                {
                    Name = "gembre",
                    Quantity = 1,
                    Unit = nameof(Unit.Gram),
                    CreateAsNewIngredient = true,
                },
            ],
            repo,
            new IngredientTextNormalizer(),
            new IngredientMatcher(repo, new IngredientTextNormalizer()),
            sut,
            CancellationToken.None);

        Assert.Single(ingredients);
        Assert.NotEqual(existingId, ingredients[0].IngredientId);
        Assert.Equal(1, repo.CreateCallCount);
    }

    private sealed class FakeIngredientRepository(
        IReadOnlyList<CanonicalIngredient> ingredients,
        IReadOnlyDictionary<string, string> aliases)
        : IIngredientRepository
    {
        public Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;

        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<CanonicalIngredient> CreateIngredientWithAliasAsync(
            string canonicalName,
            string normalizedName,
            string alias,
            string normalizedAlias,
            CancellationToken ct = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default) =>
            Task.FromResult(ingredients);

        public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default)
        {
            if (!aliases.TryGetValue(normalizedAlias, out var canonical))
            {
                return Task.FromResult<CanonicalIngredient?>(null);
            }

            return Task.FromResult(ingredients.FirstOrDefault(x => x.NormalizedName == canonical));
        }

        public Task<CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default) =>
            Task.FromResult(ingredients.SingleOrDefault(x => x.NormalizedName == normalizedName));

        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default) =>
            Task.FromResult(ingredients);

        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Tag>>([]);
    }

    private sealed class TrackingIngredientRepository(IReadOnlyList<CanonicalIngredient> ingredients) : IIngredientRepository
    {
        public int CreateCallCount { get; private set; }

        public Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default) => Task.CompletedTask;

        public Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<CanonicalIngredient> CreateIngredientWithAliasAsync(
            string canonicalName,
            string normalizedName,
            string alias,
            string normalizedAlias,
            CancellationToken ct = default)
        {
            CreateCallCount++;
            return Task.FromResult(new CanonicalIngredient
            {
                Id = Guid.NewGuid(),
                CanonicalName = canonicalName,
                NormalizedName = normalizedName,
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default) =>
            Task.FromResult(ingredients);

        public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default) =>
            Task.FromResult<CanonicalIngredient?>(null);

        public Task<CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default) =>
            Task.FromResult(ingredients.SingleOrDefault(x => x.NormalizedName == normalizedName));

        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default) =>
            Task.FromResult(ingredients);

        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Tag>>([]);
    }
}
