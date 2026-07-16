using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seeder for the curated culinary ingredient catalog.
/// Canonical display names are Dutch; English (and extra Dutch) forms become aliases.
/// </summary>
public sealed class IngredientCatalogSeeder(
    RecipeDbContext dbContext,
    IIngredientTextNormalizer normalizer,
    ILogger<IngredientCatalogSeeder> logger)
{
    public const string EmbeddedResourceName = "RecipeLibrary.Infrastructure.SeedData.curated-ingredients.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<IngredientCatalogSeedResult> SeedAsync(CancellationToken ct = default)
    {
        var catalog = LoadCatalog();
        if (catalog.Ingredients.Count == 0)
        {
            logger.LogWarning("Curated ingredient catalog is empty; nothing to seed.");
            return new IngredientCatalogSeedResult(0, 0, 0, 0);
        }

        var nameToId = await dbContext.Ingredients
            .AsNoTracking()
            .Select(x => new { x.Id, x.NormalizedName })
            .ToDictionaryAsync(x => x.NormalizedName, x => x.Id, StringComparer.Ordinal, ct);

        var existingAliases = await dbContext.IngredientAliases
            .AsNoTracking()
            .Select(x => x.NormalizedAlias)
            .ToListAsync(ct);
        var aliasSet = existingAliases.ToHashSet(StringComparer.Ordinal);

        var ingredientsInserted = 0;
        var ingredientsSkipped = 0;
        var aliasesInserted = 0;
        var aliasesSkipped = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in catalog.Ingredients)
        {
            var nlNames = entry.Names.Nl
                .Select(x => (x ?? string.Empty).Trim())
                .Where(x => x.Length > 0)
                .ToList();
            if (nlNames.Count == 0)
            {
                continue;
            }

            var canonicalName = nlNames[0];
            var normalizedCanonical = normalizer.Normalize(canonicalName);
            if (normalizedCanonical.Length == 0)
            {
                continue;
            }

            Guid ingredientId;
            if (nameToId.TryGetValue(normalizedCanonical, out var existingId))
            {
                ingredientId = existingId;
                ingredientsSkipped++;
            }
            else
            {
                ingredientId = Guid.NewGuid();
                dbContext.Ingredients.Add(new CanonicalIngredient
                {
                    Id = ingredientId,
                    CanonicalName = canonicalName,
                    NormalizedName = normalizedCanonical,
                    CreatedAt = now,
                });
                nameToId[normalizedCanonical] = ingredientId;
                ingredientsInserted++;
            }

            var aliasCandidates = new List<string>();
            aliasCandidates.AddRange(nlNames.Skip(1));
            aliasCandidates.AddRange(
                entry.Names.En
                    .Select(x => (x ?? string.Empty).Trim())
                    .Where(x => x.Length > 0));

            foreach (var alias in DeduplicateAliases(aliasCandidates, normalizer, normalizedCanonical))
            {
                var normalizedAlias = normalizer.Normalize(alias);
                if (normalizedAlias.Length == 0 || aliasSet.Contains(normalizedAlias))
                {
                    aliasesSkipped++;
                    continue;
                }

                // NormalizedName and NormalizedAlias share a uniqueness domain for matching:
                // never create an alias that collides with another canonical name.
                if (nameToId.ContainsKey(normalizedAlias) && nameToId[normalizedAlias] != ingredientId)
                {
                    aliasesSkipped++;
                    continue;
                }

                dbContext.IngredientAliases.Add(new IngredientAlias
                {
                    Id = Guid.NewGuid(),
                    IngredientId = ingredientId,
                    Alias = alias,
                    NormalizedAlias = normalizedAlias,
                });
                aliasSet.Add(normalizedAlias);
                aliasesInserted++;
            }
        }

        if (ingredientsInserted > 0 || aliasesInserted > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "Ingredient catalog seed finished. Ingredients inserted={Inserted}, skipped={Skipped}; aliases inserted={AliasInserted}, skipped={AliasSkipped}.",
            ingredientsInserted,
            ingredientsSkipped,
            aliasesInserted,
            aliasesSkipped);

        return new IngredientCatalogSeedResult(
            ingredientsInserted,
            ingredientsSkipped,
            aliasesInserted,
            aliasesSkipped);
    }

    public static CuratedIngredientCatalogDocument LoadCatalog()
    {
        var assembly = typeof(IngredientCatalogSeeder).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' was not found. Available: {string.Join(", ", assembly.GetManifestResourceNames())}");

        var document = JsonSerializer.Deserialize<CuratedIngredientCatalogDocument>(stream, JsonOptions)
            ?? throw new InvalidOperationException("Curated ingredient catalog JSON deserialized to null.");

        return document;
    }

    private static IEnumerable<string> DeduplicateAliases(
        IEnumerable<string> aliases,
        IIngredientTextNormalizer normalizer,
        string normalizedCanonical)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { normalizedCanonical };
        foreach (var alias in aliases)
        {
            var normalized = normalizer.Normalize(alias);
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                continue;
            }

            yield return alias;
        }
    }
}

public readonly record struct IngredientCatalogSeedResult(
    int IngredientsInserted,
    int IngredientsSkipped,
    int AliasesInserted,
    int AliasesSkipped);
