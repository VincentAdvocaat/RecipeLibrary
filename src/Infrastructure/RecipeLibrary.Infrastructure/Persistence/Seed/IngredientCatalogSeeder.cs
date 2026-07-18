using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seeder for the curated culinary ingredient catalog.
/// Maps catalog language keys to BCP-47 translations and language-specific aliases.
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

    private static readonly (string CatalogKey, string LanguageCode)[] LanguageMap =
    [
        ("nl", "nl"),
        ("en", "en"),
    ];

    public async Task<IngredientCatalogSeedResult> SeedAsync(CancellationToken ct = default)
    {
        var catalog = LoadCatalog();
        if (catalog.Ingredients.Count == 0)
        {
            logger.LogWarning("Curated ingredient catalog is empty; nothing to seed.");
            return new IngredientCatalogSeedResult(0, 0, 0, 0, 0, 0);
        }

        var keyToId = await dbContext.Ingredients
            .AsNoTracking()
            .Where(x => x.CatalogKey != null)
            .Select(x => new { x.Id, x.CatalogKey })
            .ToDictionaryAsync(x => x.CatalogKey!, x => x.Id, StringComparer.Ordinal, ct);

        var existingTranslations = await dbContext.IngredientTranslations
            .AsNoTracking()
            .Select(x => new { x.Id, x.IngredientId, x.LanguageCode })
            .ToListAsync(ct);
        var translationKeys = existingTranslations
            .ToDictionary(
                x => (x.IngredientId, Language: x.LanguageCode),
                x => x.Id);

        var existingAliases = await dbContext.IngredientTranslationAliases
            .AsNoTracking()
            .Select(x => new { x.IngredientTranslationId, x.NormalizedAlias })
            .ToListAsync(ct);
        var aliasKeys = existingAliases
            .Select(x => (x.IngredientTranslationId, x.NormalizedAlias))
            .ToHashSet();

        var ingredientsInserted = 0;
        var ingredientsSkipped = 0;
        var translationsInserted = 0;
        var translationsSkipped = 0;
        var aliasesInserted = 0;
        var aliasesSkipped = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var entry in catalog.Ingredients)
        {
            var catalogKey = (entry.Id ?? string.Empty).Trim();
            if (catalogKey.Length == 0)
            {
                continue;
            }

            Guid ingredientId;
            if (keyToId.TryGetValue(catalogKey, out var existingId))
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
                    CatalogKey = catalogKey,
                    UserGenerated = false,
                    CreatedAt = now,
                });
                keyToId[catalogKey] = ingredientId;
                ingredientsInserted++;
            }

            foreach (var (catalogLanguage, languageCode) in LanguageMap)
            {
                var names = GetNames(entry.Names, catalogLanguage);
                if (names.Count == 0)
                {
                    continue;
                }

                var displayName = names[0];
                var normalizedDisplay = normalizer.Normalize(displayName);
                if (normalizedDisplay.Length == 0)
                {
                    continue;
                }

                Guid translationId;
                if (translationKeys.TryGetValue((ingredientId, languageCode), out var existingTranslationId))
                {
                    translationId = existingTranslationId;
                    translationsSkipped++;
                }
                else
                {
                    translationId = Guid.NewGuid();
                    dbContext.IngredientTranslations.Add(new IngredientTranslation
                    {
                        Id = translationId,
                        IngredientId = ingredientId,
                        LanguageCode = languageCode,
                        DisplayName = displayName,
                        NormalizedDisplayName = normalizedDisplay,
                    });
                    translationKeys[(ingredientId, languageCode)] = translationId;
                    translationsInserted++;
                }

                foreach (var alias in DeduplicateAliases(names.Skip(1), normalizer, normalizedDisplay))
                {
                    var normalizedAlias = normalizer.Normalize(alias);
                    if (normalizedAlias.Length == 0
                        || aliasKeys.Contains((translationId, normalizedAlias)))
                    {
                        aliasesSkipped++;
                        continue;
                    }

                    dbContext.IngredientTranslationAliases.Add(new IngredientTranslationAlias
                    {
                        Id = Guid.NewGuid(),
                        IngredientTranslationId = translationId,
                        Alias = alias,
                        NormalizedAlias = normalizedAlias,
                    });
                    aliasKeys.Add((translationId, normalizedAlias));
                    aliasesInserted++;
                }
            }
        }

        if (ingredientsInserted > 0 || translationsInserted > 0 || aliasesInserted > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "Ingredient catalog seed finished. Ingredients inserted={Inserted}, skipped={Skipped}; translations inserted={TranslationsInserted}, skipped={TranslationsSkipped}; aliases inserted={AliasInserted}, skipped={AliasSkipped}.",
            ingredientsInserted,
            ingredientsSkipped,
            translationsInserted,
            translationsSkipped,
            aliasesInserted,
            aliasesSkipped);

        return new IngredientCatalogSeedResult(
            ingredientsInserted,
            ingredientsSkipped,
            translationsInserted,
            translationsSkipped,
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

    private static IReadOnlyList<string> GetNames(CuratedIngredientNames names, string catalogLanguage)
    {
        IReadOnlyList<string> source = catalogLanguage switch
        {
            "nl" => names.Nl,
            "en" => names.En,
            _ => [],
        };

        return source
            .Select(x => (x ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .ToList();
    }

    private static IEnumerable<string> DeduplicateAliases(
        IEnumerable<string> aliases,
        IIngredientTextNormalizer normalizer,
        string normalizedDisplay)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal) { normalizedDisplay };
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
    int TranslationsInserted,
    int TranslationsSkipped,
    int AliasesInserted,
    int AliasesSkipped);
