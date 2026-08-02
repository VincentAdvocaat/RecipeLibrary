using RecipeLibrary.Domain.Ingredients;
using Xunit;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
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

        Assert.Equal(IngredientMatchType.Alias, result.MatchType);
        Assert.Equal("gember", IngredientDisplayResolver.Resolve(result.Ingredient!, ["nl"]).DisplayName);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public async Task MatchAsync_ReturnsExactMatch_WhenDisplayNameIsPopulated()
    {
        var repo = new FakeIngredientRepository([IngredientTestFactory.Create("tomaat")]);

        var result = await CreateMatcher(repo).MatchAsync("tomaat", "nl");

        Assert.Equal(IngredientMatchType.Exact, result.MatchType);
        Assert.Equal("tomaat", IngredientDisplayResolver.Resolve(result.Ingredient!, ["nl"]).DisplayName);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public async Task MatchAsync_ReturnsFuzzyMatch_WhenAboveThreshold()
    {
        var repo = new FakeIngredientRepository([IngredientTestFactory.Create("gember")]);

        var result = await CreateMatcher(repo).MatchAsync("gembre", "nl");

        Assert.Equal(IngredientMatchType.Fuzzy, result.MatchType);
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

        Assert.Equal(IngredientMatchType.None, result.MatchType);
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

        Assert.Equal(IngredientMatchType.Exact, result.MatchType);
        Assert.Equal("tomato", IngredientDisplayResolver.Resolve(result.Ingredient!, result.LanguageChain).DisplayName);
    }

    [Fact]
    public async Task MatchAsync_ReturnsNone_ForWhitespaceInput()
    {
        var repo = new FakeIngredientRepository([IngredientTestFactory.Create("tomaat")]);

        var result = await CreateMatcher(repo).MatchAsync("   ", "nl");

        Assert.Equal(IngredientMatchType.None, result.MatchType);
        Assert.Empty(result.Suggestions);
        Assert.False(result.RequiresConfirmation);
    }

    [Fact]
    public async Task MatchAsync_ReturnsNone_WhenBestScoreEqualsFuzzyThreshold()
    {
        // Fuzzy requires score > 0.70 (strict). Equal to the threshold must stay None.
        var repo = new FakeIngredientRepository([IngredientTestFactory.Create("gember")]);
        var matcher = new IngredientMatcher(repo, new IngredientTextNormalizer(), new FixedScorer(0.70m));

        var result = await matcher.MatchAsync("gembre", "nl");

        Assert.Equal(IngredientMatchType.None, result.MatchType);
        Assert.Null(result.Ingredient);
        Assert.Equal(0m, result.Confidence);
        Assert.Contains(result.Suggestions, x => x.Display.DisplayName == "gember");
        Assert.All(result.Suggestions, x => Assert.Equal(0.70m, x.Score));
    }

    [Fact]
    public async Task MatchAsync_ReturnsFuzzy_WhenBestScoreJustAboveFuzzyThreshold()
    {
        var repo = new FakeIngredientRepository([IngredientTestFactory.Create("gember")]);
        var matcher = new IngredientMatcher(repo, new IngredientTextNormalizer(), new FixedScorer(0.71m));

        var result = await matcher.MatchAsync("gembre", "nl");

        Assert.Equal(IngredientMatchType.Fuzzy, result.MatchType);
        Assert.Equal(0.71m, result.Confidence);
    }

    [Fact]
    public async Task MatchAsync_LimitsSuggestionsToMaxSuggestions()
    {
        // Names close enough that full-string similarity still allows fuzzy auto-accept.
        var ingredients = Enumerable.Range(1, 8)
            .Select(i => IngredientTestFactory.Create($"gember{i}"))
            .ToList();
        var repo = new FakeIngredientRepository(ingredients);
        var matcher = new IngredientMatcher(repo, new IngredientTextNormalizer(), new FixedScorer(0.80m));

        var result = await matcher.MatchAsync("gemberx", "nl");

        Assert.Equal(IngredientMatcher.MaxSuggestions, result.Suggestions.Count);
        Assert.Equal(IngredientMatchType.Fuzzy, result.MatchType);
    }

    [Fact]
    public async Task MatchAsync_PrefersShorterDisplayName_WhenScoresTie()
    {
        var shortName = IngredientTestFactory.Create("gember");
        var longName = IngredientTestFactory.Create("gemberwortel");
        var repo = new FakeIngredientRepository([longName, shortName]);
        var matcher = new IngredientMatcher(repo, new IngredientTextNormalizer(), new FixedScorer(0.80m));

        var result = await matcher.MatchAsync("gembre", "nl");

        Assert.Equal("gember", result.Suggestions[0].Display.DisplayName);
        Assert.Equal(IngredientMatchType.Fuzzy, result.MatchType);
        Assert.Equal(shortName.Id, result.Ingredient!.Id);
    }

    [Fact]
    public async Task MatchAsync_ReturnsNone_WhenFuzzyCandidatesEmpty_WithoutCatalogFallback()
    {
        var tomato = IngredientTestFactory.Create("tomaat");
        var repo = new FakeIngredientRepository([tomato], fuzzyCandidates: []);
        var matcher = new IngredientMatcher(repo, new IngredientTextNormalizer(), new FixedScorer(0.80m));

        // Too short for prefix fallback — must not call SearchAsync("", …).
        var result = await matcher.MatchAsync("xy", "nl");

        Assert.Equal(IngredientMatchType.None, result.MatchType);
        Assert.Null(result.Ingredient);
        Assert.Empty(result.Suggestions);
        Assert.False(repo.SearchWasCalled);
    }

    [Fact]
    public async Task MatchAsync_UsesPrefixSearch_NotEmptyCatalog_WhenFuzzyCandidatesEmpty()
    {
        var tomato = IngredientTestFactory.Create("tomaat");
        var repo = new FakeIngredientRepository([tomato], fuzzyCandidates: []);
        var matcher = new IngredientMatcher(repo, new IngredientTextNormalizer(), new FixedScorer(0.80m));

        var result = await matcher.MatchAsync("xyzunrelated", "nl");

        Assert.Equal(IngredientMatchType.None, result.MatchType);
        Assert.Null(result.Ingredient);
        Assert.True(repo.SearchWasCalled);
        Assert.Equal("xyz", repo.LastSearchQuery);
    }

    [Fact]
    public async Task MatchAsync_DoesNotAutoAccept_WhenOnlySharedExactToken()
    {
        var runderGehakt = IngredientTestFactory.Create("runder gehakt");
        var repo = new FakeIngredientRepository([runderGehakt]);

        var result = await CreateMatcher(repo).MatchAsync("gehakt", "nl");

        Assert.Equal(IngredientMatchType.None, result.MatchType);
        Assert.Null(result.Ingredient);
        Assert.True(result.RequiresConfirmation);
        Assert.Contains(result.Suggestions, x => x.Display.DisplayName == "runder gehakt");
    }

    private static IngredientMatcher CreateMatcher(IIngredientRepository repo) =>
        new(repo, new IngredientTextNormalizer(), new IngredientSimilarityScorer());

    private sealed class FixedScorer(decimal score) : IIngredientSimilarityScorer
    {
        public decimal Score(string normalizedInput, string normalizedCandidate) => score;
    }

    private sealed class FakeIngredientRepository(
        IReadOnlyList<CanonicalIngredient> ingredients,
        IReadOnlyList<CanonicalIngredient>? fuzzyCandidates = null)
        : IIngredientRepository
    {
        public bool SearchWasCalled { get; private set; }
        public string? LastSearchQuery { get; private set; }

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
            CancellationToken ct = default)
        {
            if (fuzzyCandidates is not null)
            {
                return Task.FromResult(fuzzyCandidates);
            }

            return Task.FromResult<IReadOnlyList<CanonicalIngredient>>(
                ingredients
                    .Where(x => x.Translations.Any(t =>
                        languageCodes.Contains(t.LanguageCode, StringComparer.OrdinalIgnoreCase)
                        && IngredientCandidateMatcher.Matches(normalizedQuery, t.NormalizedDisplayName)))
                    .Take(take)
                    .ToList());
        }

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
            CancellationToken ct = default)
        {
            SearchWasCalled = true;
            LastSearchQuery = normalizedQuery;
            return Task.FromResult<IReadOnlyList<CanonicalIngredient>>(
                string.IsNullOrWhiteSpace(normalizedQuery)
                    ? ingredients.Take(take).ToList()
                    : ingredients
                        .Where(x => x.Translations.Any(t =>
                            languageCodes.Contains(t.LanguageCode, StringComparer.OrdinalIgnoreCase)
                            && IngredientCandidateMatcher.Matches(normalizedQuery, t.NormalizedDisplayName)))
                        .Take(take)
                        .ToList());
        }

        public Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tag>>([]);
    }
}
