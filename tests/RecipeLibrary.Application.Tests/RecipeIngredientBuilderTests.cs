using System.Globalization;
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
        var canonical = IngredientTestFactory.Create("Wortel", id: canonicalId);
        var repo = new FakeIngredientRepository([canonical]);
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
            new IngredientMatcher(repo, new IngredientTextNormalizer(), new IngredientSimilarityScorer()),
            sut,
            CancellationToken.None);

        Assert.Single(ingredients);
        Assert.Equal("Wortel", ingredients[0].Name);
        Assert.Equal("in blokjes", ingredients[0].Preparation);
        Assert.Equal(canonicalId, ingredients[0].IngredientId);
    }

    [Fact]
    public async Task BuildAsync_FuzzyMatch_LinksExistingIngredient()
    {
        var recipeId = Guid.NewGuid();
        var existingId = Guid.NewGuid();
        var existing = IngredientTestFactory.Create("gember", id: existingId);
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
                },
            ],
            repo,
            new IngredientTextNormalizer(),
            new IngredientMatcher(repo, new IngredientTextNormalizer(), new IngredientSimilarityScorer()),
            sut,
            CancellationToken.None);

        Assert.Single(ingredients);
        Assert.Equal(existingId, ingredients[0].IngredientId);
        Assert.Equal(0, repo.CreateCallCount);
    }

    [Fact]
    public async Task BuildAsync_UnmatchedIngredient_UsesStorageLanguageCode()
    {
        var previousUi = CultureInfo.CurrentUICulture;
        var previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("nl-NL");
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("nl-NL");

            var recipeId = Guid.NewGuid();
            var repo = new LanguageTrackingIngredientRepository();
            var sut = new IngredientLineResolver(new IngredientNameParser());

            await RecipeIngredientBuilder.BuildAsync(
                recipeId,
                [
                    new CreateRecipeIngredientDto
                    {
                        Name = "xyzunmatchedingredient99",
                        Quantity = 1,
                        Unit = nameof(Unit.Gram),
                    },
                ],
                repo,
                new IngredientTextNormalizer(),
                new IngredientMatcher(repo, new IngredientTextNormalizer(), new IngredientSimilarityScorer()),
                sut,
                CancellationToken.None);

            Assert.Equal("nl", repo.LastLanguageCode);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousUi;
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    private sealed class LanguageTrackingIngredientRepository : IngredientRepositoryStub
    {
        public string? LastLanguageCode { get; private set; }

        public override Task<CanonicalIngredient> FindOrCreateAsync(
            string languageCode,
            string displayName,
            string normalizedDisplayName,
            string? alias,
            string? normalizedAlias,
            CancellationToken ct = default)
        {
            LastLanguageCode = languageCode;
            return Task.FromResult(IngredientTestFactory.Create(displayName, languageCode));
        }
    }

    private sealed class FakeIngredientRepository(IReadOnlyList<CanonicalIngredient> ingredients)
        : IngredientRepositoryStub
    {
        public override Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(
            string normalizedQuery,
            IReadOnlyList<string> languageCodes,
            int take,
            CancellationToken ct = default) =>
            Task.FromResult(ingredients);

        public override Task<CanonicalIngredient?> GetByNormalizedNameAsync(
            string normalizedName,
            IReadOnlyList<string> languageCodes,
            CancellationToken ct = default) =>
            Task.FromResult(ingredients.FirstOrDefault(x =>
                x.Translations.Any(t => t.NormalizedDisplayName == normalizedName)));

        public override Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(
            string normalizedQuery,
            IReadOnlyList<string> languageCodes,
            int take,
            CancellationToken ct = default) =>
            Task.FromResult(ingredients);
    }

    private sealed class TrackingIngredientRepository(IReadOnlyList<CanonicalIngredient> ingredients)
        : IngredientRepositoryStub
    {
        public int CreateCallCount { get; private set; }

        public override Task<CanonicalIngredient> FindOrCreateAsync(
            string languageCode,
            string displayName,
            string normalizedDisplayName,
            string? alias,
            string? normalizedAlias,
            CancellationToken ct = default)
        {
            CreateCallCount++;
            return Task.FromResult(IngredientTestFactory.Create(displayName, languageCode));
        }

        public override Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(
            string normalizedQuery,
            IReadOnlyList<string> languageCodes,
            int take,
            CancellationToken ct = default) =>
            Task.FromResult(ingredients);

        public override Task<CanonicalIngredient?> GetByNormalizedNameAsync(
            string normalizedName,
            IReadOnlyList<string> languageCodes,
            CancellationToken ct = default) =>
            Task.FromResult(ingredients.FirstOrDefault(x =>
                x.Translations.Any(t => t.NormalizedDisplayName == normalizedName)));

        public override Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(
            string normalizedQuery,
            IReadOnlyList<string> languageCodes,
            int take,
            CancellationToken ct = default) =>
            Task.FromResult(ingredients);
    }
}
