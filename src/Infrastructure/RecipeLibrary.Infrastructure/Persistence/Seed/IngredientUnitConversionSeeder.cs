using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seeder for conversion sources and curated kitchen→mass conversions.
/// </summary>
public sealed class IngredientUnitConversionSeeder(
    RecipeDbContext dbContext,
    ILogger<IngredientUnitConversionSeeder> logger)
{
    public const string EmbeddedResourceName = "RecipeLibrary.Infrastructure.SeedData.unit-conversions.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task SeedAsync(CancellationToken ct = default)
    {
        var document = LoadDocument();
        if (document.Sources.Count == 0 && document.Conversions.Count == 0)
        {
            logger.LogWarning("Unit conversion seed document is empty; nothing to seed.");
            return;
        }

        var sourceIds = await EnsureSourcesAsync(document.Sources, ct);
        var catalogIds = await dbContext.Ingredients
            .AsNoTracking()
            .Where(x => x.CatalogKey != null)
            .Select(x => new { x.Id, x.CatalogKey })
            .ToDictionaryAsync(x => x.CatalogKey!, x => x.Id, StringComparer.Ordinal, ct);

        var existingKeys = await dbContext.IngredientUnitConversions
            .AsNoTracking()
            .Select(x => new { x.CanonicalIngredientId, x.FromUnit, x.ToUnit, x.ConversionSourceId })
            .ToListAsync(ct);
        var existing = existingKeys
            .Select(x => (x.CanonicalIngredientId, x.FromUnit, x.ToUnit, x.ConversionSourceId))
            .ToHashSet();

        var inserted = 0;
        var skipped = 0;
        var now = DateTimeOffset.UtcNow;

        foreach (var row in document.Conversions)
        {
            var catalogKey = (row.CatalogKey ?? string.Empty).Trim();
            var sourceName = (row.Source ?? string.Empty).Trim();
            if (catalogKey.Length == 0
                || sourceName.Length == 0
                || !catalogIds.TryGetValue(catalogKey, out var ingredientId)
                || !sourceIds.TryGetValue(sourceName, out var sourceId)
                || !UnitRules.TryParse(row.FromUnit, out var fromUnit)
                || !UnitRules.TryParse(row.ToUnit, out var toUnit)
                || row.AmountFrom <= 0
                || row.AmountTo <= 0)
            {
                skipped++;
                continue;
            }

            var key = (ingredientId, fromUnit, toUnit, sourceId);
            if (existing.Contains(key))
            {
                skipped++;
                continue;
            }

            dbContext.IngredientUnitConversions.Add(new IngredientUnitConversion
            {
                Id = Guid.NewGuid(),
                CanonicalIngredientId = ingredientId,
                FromUnit = fromUnit,
                ToUnit = toUnit,
                AmountFrom = row.AmountFrom,
                AmountTo = row.AmountTo,
                ConversionSourceId = sourceId,
                Origin = ConversionOrigin.Curated,
                ExternalReference = string.IsNullOrWhiteSpace(row.ExternalReference)
                    ? null
                    : row.ExternalReference.Trim(),
                Notes = string.IsNullOrWhiteSpace(row.Notes) ? null : row.Notes.Trim(),
                CreatedAt = now,
            });
            existing.Add(key);
            inserted++;
        }

        if (inserted > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }

        logger.LogInformation(
            "Unit conversion seed finished. Inserted={Inserted}, Skipped={Skipped}.",
            inserted,
            skipped);
    }

    private async Task<Dictionary<string, Guid>> EnsureSourcesAsync(
        IReadOnlyList<ConversionSourceSeedEntry> sources,
        CancellationToken ct)
    {
        var names = sources
            .Select(x => (x.Name ?? string.Empty).Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (names.Count == 0)
        {
            names =
            [
                ConversionSourceNames.KingArthur,
                ConversionSourceNames.Usda,
                ConversionSourceNames.Manual,
            ];
        }

        var existing = await dbContext.ConversionSources.ToListAsync(ct);
        var byName = existing.ToDictionary(x => x.Name, x => x.Id, StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            if (byName.ContainsKey(name))
            {
                continue;
            }

            var entity = new ConversionSource { Id = Guid.NewGuid(), Name = name };
            dbContext.ConversionSources.Add(entity);
            byName[name] = entity.Id;
        }

        await dbContext.SaveChangesAsync(ct);
        return byName;
    }

    private static UnitConversionSeedDocument LoadDocument()
    {
        var assembly = typeof(IngredientUnitConversionSeeder).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{EmbeddedResourceName}' was not found.");
        using var reader = new StreamReader(stream);
        var json = reader.ReadToEnd();
        return JsonSerializer.Deserialize<UnitConversionSeedDocument>(json, JsonOptions)
            ?? new UnitConversionSeedDocument();
    }

    private sealed class UnitConversionSeedDocument
    {
        public List<ConversionSourceSeedEntry> Sources { get; set; } = [];

        public List<UnitConversionSeedEntry> Conversions { get; set; } = [];
    }

    private sealed class ConversionSourceSeedEntry
    {
        public string? Name { get; set; }
    }

    private sealed class UnitConversionSeedEntry
    {
        public string? CatalogKey { get; set; }

        public string? FromUnit { get; set; }

        public string? ToUnit { get; set; }

        public decimal AmountFrom { get; set; }

        public decimal AmountTo { get; set; }

        public string? Source { get; set; }

        public string? ExternalReference { get; set; }

        public string? Notes { get; set; }
    }
}
