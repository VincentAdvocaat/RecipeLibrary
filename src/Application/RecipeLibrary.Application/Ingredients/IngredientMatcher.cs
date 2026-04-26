using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Ingredients;

public sealed class IngredientMatcher(IIngredientRepository ingredientRepository, IIngredientTextNormalizer normalizer)
{
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
            .Select(x => new { Ingredient = x, Score = Similarity(normalized, x.NormalizedName) })
            .OrderByDescending(x => x.Score)
            .ToList();

        var best = scored.FirstOrDefault();
        if (best is not null && best.Score > 0.7m)
        {
            return IngredientMatchResult.Fuzzy(normalized, best.Ingredient, best.Score, scored.Take(5).Select(x => x.Ingredient).ToList());
        }

        return IngredientMatchResult.None(normalized, scored.Take(5).Select(x => x.Ingredient).ToList());
    }

    private static decimal Similarity(string a, string b)
    {
        return Math.Max(LevenshteinSimilarity(a, b), JaroWinklerSimilarity(a, b));
    }

    private static decimal LevenshteinSimilarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0) return 1m;
        var maxLength = Math.Max(a.Length, b.Length);
        var distance = LevenshteinDistance(a, b);
        return maxLength == 0 ? 1m : 1m - (decimal)distance / maxLength;
    }

    private static int LevenshteinDistance(string a, string b)
    {
        var rows = a.Length + 1;
        var cols = b.Length + 1;
        var matrix = new int[rows, cols];

        for (var i = 0; i < rows; i++) matrix[i, 0] = i;
        for (var j = 0; j < cols; j++) matrix[0, j] = j;

        for (var i = 1; i < rows; i++)
        {
            for (var j = 1; j < cols; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                matrix[i, j] = Math.Min(
                    Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                    matrix[i - 1, j - 1] + cost);
            }
        }

        return matrix[a.Length, b.Length];
    }

    private static decimal JaroWinklerSimilarity(string s1, string s2)
    {
        if (s1 == s2) return 1m;
        if (s1.Length == 0 || s2.Length == 0) return 0m;

        var matchDistance = Math.Max(s1.Length, s2.Length) / 2 - 1;
        var s1Matches = new bool[s1.Length];
        var s2Matches = new bool[s2.Length];
        var matches = 0;

        for (var i = 0; i < s1.Length; i++)
        {
            var start = Math.Max(0, i - matchDistance);
            var end = Math.Min(i + matchDistance + 1, s2.Length);
            for (var j = start; j < end; j++)
            {
                if (s2Matches[j] || s1[i] != s2[j]) continue;
                s1Matches[i] = true;
                s2Matches[j] = true;
                matches++;
                break;
            }
        }

        if (matches == 0) return 0m;

        decimal transpositions = 0;
        var k = 0;
        for (var i = 0; i < s1.Length; i++)
        {
            if (!s1Matches[i]) continue;
            while (!s2Matches[k]) k++;
            if (s1[i] != s2[k]) transpositions++;
            k++;
        }

        transpositions /= 2m;
        var m = (decimal)matches;
        var jaro = ((m / s1.Length) + (m / s2.Length) + ((m - transpositions) / m)) / 3m;

        var prefix = 0;
        for (var i = 0; i < Math.Min(4, Math.Min(s1.Length, s2.Length)); i++)
        {
            if (s1[i] == s2[i]) prefix++;
            else break;
        }

        return jaro + (prefix * 0.1m * (1m - jaro));
    }
}

public sealed record IngredientMatchResult(
    string MatchType,
    CanonicalIngredient? Ingredient,
    decimal Confidence,
    string NormalizedInput,
    IReadOnlyList<CanonicalIngredient> Suggestions)
{
    public static IngredientMatchResult Exact(string normalizedInput, CanonicalIngredient ingredient) =>
        new("exact", ingredient, 1m, normalizedInput, []);

    public static IngredientMatchResult Alias(string normalizedInput, CanonicalIngredient ingredient) =>
        new("alias", ingredient, 0.95m, normalizedInput, []);

    public static IngredientMatchResult Fuzzy(string normalizedInput, CanonicalIngredient ingredient, decimal confidence, IReadOnlyList<CanonicalIngredient> suggestions) =>
        new("fuzzy", ingredient, confidence, normalizedInput, suggestions);

    public static IngredientMatchResult None(string normalizedInput, IReadOnlyList<CanonicalIngredient> suggestions) =>
        new("none", null, 0m, normalizedInput, suggestions);
}
