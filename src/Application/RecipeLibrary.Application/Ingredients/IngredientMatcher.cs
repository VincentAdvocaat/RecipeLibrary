using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Ingredients;

public sealed class IngredientMatcher(
    IIngredientRepository ingredientRepository,
    IIngredientTextNormalizer normalizer,
    IngredientSimilarityScorer similarityScorer)
{
    public const decimal SuggestionMinScore = 0.45m;
    public const decimal FuzzyMatchScore = 0.70m;
    public const int MaxSuggestions = 5;

    public async Task<IngredientMatchResult> MatchAsync(string? input, CancellationToken ct = default)
    {
        var raw = (input ?? string.Empty).Trim();
        var normalized = normalizer.Normalize(raw);
        if (normalized.Length == 0)
        {
            return IngredientMatchResult.None(normalized, []);
        }

        var exact = await ingredientRepository.GetByNormalizedNameAsync(normalized, ct);
        if (exact is not null)
        {
            return IngredientMatchResult.Exact(normalized, exact);
        }

        var alias = await ingredientRepository.GetByNormalizedAliasAsync(normalized, ct);
        if (alias is not null)
        {
            return IngredientMatchResult.Alias(normalized, alias);
        }

        var candidates = await ingredientRepository.GetFuzzyCandidatesAsync(normalized, 40, ct);
        if (candidates.Count == 0)
        {
            candidates = await ingredientRepository.SearchAsync(string.Empty, 40, ct);
        }

        var scored = candidates
            .Select(x => new ScoredIngredientSuggestion(x, similarityScorer.Score(normalized, x.NormalizedName)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Ingredient.NormalizedName.Length)
            .ThenBy(x => x.Ingredient.CanonicalName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var filteredSuggestions = scored
            .Where(x => x.Score >= SuggestionMinScore)
            .Take(MaxSuggestions)
            .ToList();

        var best = filteredSuggestions.FirstOrDefault();
        if (best is not null && best.Score > FuzzyMatchScore)
        {
            return IngredientMatchResult.Fuzzy(normalized, best.Ingredient, best.Score, filteredSuggestions);
        }

        return IngredientMatchResult.None(normalized, filteredSuggestions);
    }
}

public sealed record ScoredIngredientSuggestion(CanonicalIngredient Ingredient, decimal Score);

public sealed record IngredientMatchResult(
    string MatchType,
    CanonicalIngredient? Ingredient,
    decimal Confidence,
    string NormalizedInput,
    IReadOnlyList<ScoredIngredientSuggestion> Suggestions,
    bool RequiresConfirmation)
{
    public static IngredientMatchResult Exact(string normalizedInput, CanonicalIngredient ingredient) =>
        new("exact", ingredient, 1m, normalizedInput, [], false);

    public static IngredientMatchResult Alias(string normalizedInput, CanonicalIngredient ingredient) =>
        new("alias", ingredient, 0.95m, normalizedInput, [], false);

    public static IngredientMatchResult Fuzzy(
        string normalizedInput,
        CanonicalIngredient ingredient,
        decimal confidence,
        IReadOnlyList<ScoredIngredientSuggestion> suggestions) =>
        new("fuzzy", ingredient, confidence, normalizedInput, suggestions, suggestions.Count > 0);

    public static IngredientMatchResult None(string normalizedInput, IReadOnlyList<ScoredIngredientSuggestion> suggestions) =>
        new("none", null, 0m, normalizedInput, suggestions, suggestions.Count > 0);
}
