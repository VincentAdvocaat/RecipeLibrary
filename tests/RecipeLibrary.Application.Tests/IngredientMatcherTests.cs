using Xunit;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Tests;

public sealed class IngredientMatcherTests
{
    private static readonly IReadOnlyList<CanonicalIngredient> GehaktIngredients =
    [
        IngredientTestFactory.Create("gehakt"),
        IngredientTestFactory.Create("runder gehakt"),
    ];

    [Fact]
    public async Task MatchAsync_UsesAliasBeforeFuzzy()
    {
        var gember = IngredientTestFactory.Create("gember", aliases: "verse gember");
        var repo = new FakeIngredientRepository([gember]);

        var result = await CreateMatcher(repo).MatchAsync("verse gember", "nl");

        Assert.Equal("alias", result.MatchType);
        Assert.Equal("gember", IngredientDisplayResolver.Resolve(result.Ingredient!, ["nl"]).DisplayName);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public async Task MatchAsync_ReturnsExactMatch_WhenDisplayNameIsPopulated()
    {
        var repo = new FakeIngredientRepository([IngredientTestFactory.Create("tomaat")]);

        var result = await CreateMatcher(repo).MatchAsync("tomaat", "nl");

        Assert.Equal("exact", result.MatchType);
        Assert.Equal("tomaat", IngredientDisplayResolver.Resolve(result.Ingredient!, ["nl"]).DisplayName);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public async Task MatchAsync_ReturnsFuzzyMatch_WhenAboveThreshold()
    {
        var repo = new FakeIngredientRepository([IngredientTestFactory.Create("gember")]);

        var result = await CreateMatcher(repo).MatchAsync("gembre", "nl");

        Assert.Equal("fuzzy", result.MatchType);
        Assert.Equal("gember", IngredientDisplayResolver.Resolve(result.Ingredient!, ["nl"]).DisplayName);
        Assert.True(result.Confidence > IngredientMatcher.FuzzyMatchScore);
        Assert.True(result.RequiresConfirmation);
        Assert.Contains(result.Suggestions, x => x.Display.DisplayName == "gember");
    }

    [Fact]
    public async Task MatchAsync_RequiresConfirmation_WhenCloseSuggestionsExist()
    {
        var repo = new FakeIngredientRepository([IngredientTestFactory.Create("gember")]);

        var result = await CreateMatcher(repo).MatchAsync("gembr", "nl");

        Assert.True(result.RequiresConfirmation);
        Assert.NotEmpty(result.Suggestions);
    }

    [Fact]
    public async Task MatchAsync_DoesNotRequireConfirmation_WhenNoCloseSuggestionsExist()
    {
        var repo = new FakeIngredientRepository([IngredientTestFactory.Create("gember")]);

        var result = await CreateMatcher(repo).MatchAsync("xyzabc123", "nl");

        Assert.Equal("none", result.MatchType);
        Assert.False(result.RequiresConfirmation);
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public async Task MatchAsync_FiltersSuggestionsBelowMinScore()
    {
        var repo = new FakeIngredientRepository(
        [
            IngredientTestFactory.Create("gember"),
            IngredientTestFactory.Create("aardappel"),
        ]);

        var result = await CreateMatcher(repo).MatchAsync("xyzabc123", "nl");

        Assert.All(result.Suggestions, x => Assert.True(x.Score >= IngredientMatcher.SuggestionMinScore));
    }

    [Fact]
    public async Task MatchAsync_SuggestsGehaktAndRunderGehakt_WhenInputIsGehak()
    {
        var repo = new FakeIngredientRepository(GehaktIngredients);

        var result = await CreateMatcher(repo).MatchAsync("gehak", "nl");

        Assert.True(result.RequiresConfirmation);
        Assert.Contains(result.Suggestions, x => x.Display.DisplayName == "gehakt");
        Assert.Contains(result.Suggestions, x => x.Display.DisplayName == "runder gehakt");
        Assert.All(result.Suggestions, x => Assert.True(x.Score >= IngredientMatcher.SuggestionMinScore));
    }

    [Fact]
    public async Task MatchAsync_SuggestsRunderGehakt_WhenInputIsGehakt()
    {
        var repo = new FakeIngredientRepository([GehaktIngredients[1]]);

        var result = await CreateMatcher(repo).MatchAsync("gehakt", "nl");

        Assert.Contains(result.Suggestions, x => x.Display.DisplayName == "runder gehakt");
        Assert.True(result.RequiresConfirmation);
    }

    [Fact]
    public async Task MatchAsync_SuggestsGehakt_WhenInputIsRunderGehakt()
    {
        var repo = new FakeIngredientRepository([GehaktIngredients[0]]);

        var result = await CreateMatcher(repo).MatchAsync("runder gehakt", "nl");

        Assert.Contains(result.Suggestions, x => x.Display.DisplayName == "gehakt");
        Assert.True(result.RequiresConfirmation);
    }

    [Fact]
    public async Task MatchAsync_UsesEnglishTranslation_WhenCultureIsEnglish()
    {
        var tomato = IngredientTestFactory.Create("tomaat", "nl", catalogKey: "tomato");
        tomato.Translations.Add(new IngredientTranslation
        {
            Id = Guid.NewGuid(),
            IngredientId = tomato.Id,
            LanguageCode = "en",
            DisplayName = "tomato",
            NormalizedDisplayName = "tomato",
        });
        var repo = new FakeIngredientRepository([tomato]);

        var result = await CreateMatcher(repo).MatchAsync("tomato", "en-US");

        Assert.Equal("exact", result.MatchType);
        Assert.Equal("tomato", IngredientDisplayResolver.Resolve(result.Ingredient!, result.LanguageChain).DisplayName);
    }

    private static IngredientMatcher CreateMatcher(IIngredientRepository repo) =>
        new(repo, new IngredientTextNormalizer(), new IngredientSimilarityScorer());

    private sealed class FakeIngredientRepository(IReadOnlyList<CanonicalIngredient> ingredients)
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
            => Task.FromResult(IngredientTestFactory.Create(displayName, languageCode));

        public Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(
            string normalizedQuery,
            IReadOnlyList<string> languageCodes,
            int take,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CanonicalIngredient>>(
                ingredients
                    .Where(x => x.Translations.Any(t =>
                        languageCodes.Contains(t.LanguageCode, StringComparer.OrdinalIgnoreCase)
                        && IngredientCandidateMatcher.Matches(normalizedQuery, t.NormalizedDisplayName)))
                    .Take(take)
                    .ToList());

        public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(
            string normalizedAlias,
            IReadOnlyList<string> languageCodes,
            CancellationToken ct = default)
        {
            foreach (var language in languageCodes)
            {
                var match = ingredients.FirstOrDefault(x =>
                    x.Translations.Any(t =>
                        string.Equals(t.LanguageCode, language, StringComparison.OrdinalIgnoreCase)
                        && t.Aliases.Any(a => a.NormalizedAlias == normalizedAlias)));
                if (match is not null)
                {
                    return Task.FromResult<CanonicalIngredient?>(match);
                }
            }

            return Task.FromResult<CanonicalIngredient?>(null);
        }

        public Task<CanonicalIngredient?> GetByNormalizedNameAsync(
            string normalizedName,
            IReadOnlyList<string> languageCodes,
            CancellationToken ct = default)
        {
            foreach (var language in languageCodes)
            {
                var match = ingredients.FirstOrDefault(x =>
                    x.Translations.Any(t =>
                        string.Equals(t.LanguageCode, language, StringComparison.OrdinalIgnoreCase)
                        && t.NormalizedDisplayName == normalizedName));
                if (match is not null)
                {
                    return Task.FromResult<CanonicalIngredient?>(match);
                }
            }

            return Task.FromResult<CanonicalIngredient?>(null);
        }

        public Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(
            string normalizedQuery,
            IReadOnlyList<string> languageCodes,
            int take,
            CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CanonicalIngredient>>(
                string.IsNullOrWhiteSpace(normalizedQuery)
                    ? ingredients.Take(take).ToList()
                    : ingredients
                        .Where(x => x.Translations.Any(t =>
                            languageCodes.Contains(t.LanguageCode, StringComparer.OrdinalIgnoreCase)
                            && IngredientCandidateMatcher.Matches(normalizedQuery, t.NormalizedDisplayName)))
                        .Take(take)
                        .ToList());

        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tag>>([]);
    }
}
