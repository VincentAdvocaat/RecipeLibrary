using System.Globalization;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Ingredients;

public sealed class IngredientMatcher(
    IIngredientRepository ingredientRepository,
    IIngredientTextNormalizer normalizer,
    IIngredientSimilarityScorer similarityScorer)
{
    public const decimal SuggestionMinScore = 0.45m;
    public const decimal FuzzyMatchScore = 0.70m;
    public const int MaxSuggestions = 5;

    public Task<IngredientMatchResult> MatchAsync(string? input, CancellationToken ct = default) =>
        MatchAsync(input, CultureInfo.CurrentUICulture.Name, ct);

    public async Task<IngredientMatchResult> MatchAsync(
        string? input,
        string? cultureName,
        CancellationToken ct = default)
    {
        var raw = (input ?? string.Empty).Trim();
        var normalized = normalizer.Normalize(raw);
        var languageChain = IngredientLanguageFallback.ResolveChain(cultureName);
        if (normalized.Length == 0)
        {
            return IngredientMatchResult.None(normalized, languageChain, []);
        }

        var exact = await ingredientRepository.GetByNormalizedNameAsync(normalized, languageChain, ct);
        if (exact is not null)
        {
            return IngredientMatchResult.Exact(normalized, languageChain, exact);
        }

        var alias = await ingredientRepository.GetByNormalizedAliasAsync(normalized, languageChain, ct);
        if (alias is not null)
        {
            return IngredientMatchResult.Alias(normalized, languageChain, alias);
        }

        var candidates = await ingredientRepository.GetFuzzyCandidatesAsync(normalized, languageChain, 40, ct);
        if (candidates.Count == 0 && normalized.Length >= 3)
        {
            // Narrow prefix search for typos (e.g. "gembre" → candidates starting with "gem").
            // Never fall back to SearchAsync("", …) which scores unrelated catalog rows.
            candidates = await ingredientRepository.SearchAsync(normalized[..3], languageChain, 40, ct);
        }

        if (candidates.Count == 0)
        {
            return IngredientMatchResult.None(normalized, languageChain, []);
        }

        var scored = candidates
            .Select(x =>
            {
                var scoreName = IngredientDisplayResolver.ResolveNormalizedDisplayName(x, languageChain)
                    ?? string.Empty;
                return new ScoredIngredientSuggestion(
                    x,
                    similarityScorer.Score(normalized, scoreName),
                    IngredientDisplayResolver.Resolve(x, languageChain));
            })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => IngredientDisplayResolver.ResolveNormalizedDisplayName(x.Ingredient, languageChain)?.Length ?? int.MaxValue)
            .ThenBy(x => x.Display.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filteredSuggestions = scored
            .Where(x => x.Score >= SuggestionMinScore)
            .Take(MaxSuggestions)
            .ToList();

        var best = filteredSuggestions.FirstOrDefault();
        if (best is not null && best.Score > FuzzyMatchScore)
        {
            // Shared-token / subset boosts can exceed FuzzyMatchScore without a close full-string
            // match; only auto-accept when the whole names are similar enough (true typos).
            var bestName = IngredientDisplayResolver.ResolveNormalizedDisplayName(best.Ingredient, languageChain)
                ?? string.Empty;
            var fullSimilarity = IngredientSimilarityScorer.StringSimilarity(normalized, bestName);
            if (fullSimilarity > FuzzyMatchScore)
            {
                return IngredientMatchResult.Fuzzy(
                    normalized,
                    languageChain,
                    best.Ingredient,
                    best.Score,
                    filteredSuggestions);
            }
        }

        return IngredientMatchResult.None(normalized, languageChain, filteredSuggestions);
    }
}

public sealed record ScoredIngredientSuggestion(
    CanonicalIngredient Ingredient,
    decimal Score,
    IngredientDisplay Display);

public sealed record IngredientMatchResult(
    string MatchType,
    CanonicalIngredient? Ingredient,
    decimal Confidence,
    string NormalizedInput,
    IReadOnlyList<string> LanguageChain,
    IReadOnlyList<ScoredIngredientSuggestion> Suggestions,
    bool RequiresConfirmation)
{
    public static IngredientMatchResult Exact(
        string normalizedInput,
        IReadOnlyList<string> languageChain,
        CanonicalIngredient ingredient) =>
        new("exact", ingredient, 1m, normalizedInput, languageChain, [], false);

    public static IngredientMatchResult Alias(
        string normalizedInput,
        IReadOnlyList<string> languageChain,
        CanonicalIngredient ingredient) =>
        new("alias", ingredient, 0.95m, normalizedInput, languageChain, [], false);

    public static IngredientMatchResult Fuzzy(
        string normalizedInput,
        IReadOnlyList<string> languageChain,
        CanonicalIngredient ingredient,
        decimal confidence,
        IReadOnlyList<ScoredIngredientSuggestion> suggestions) =>
        new("fuzzy", ingredient, confidence, normalizedInput, languageChain, suggestions, suggestions.Count > 0);

    public static IngredientMatchResult None(
        string normalizedInput,
        IReadOnlyList<string> languageChain,
        IReadOnlyList<ScoredIngredientSuggestion> suggestions) =>
        new("none", null, 0m, normalizedInput, languageChain, suggestions, suggestions.Count > 0);
}
