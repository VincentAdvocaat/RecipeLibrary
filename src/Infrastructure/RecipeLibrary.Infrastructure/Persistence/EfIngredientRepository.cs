using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class EfIngredientRepository(RecipeDbContext dbContext) : IIngredientRepository
{
    public async Task<CanonicalIngredient?> GetByNormalizedNameAsync(
        string normalizedName,
        IReadOnlyList<string> languageCodes,
        CancellationToken ct = default)
    {
        foreach (var language in languageCodes)
        {
            var ingredientId = await dbContext.IngredientTranslations
                .AsNoTracking()
                .Where(x => x.LanguageCode == language && x.NormalizedDisplayName == normalizedName)
                .Select(x => x.IngredientId)
                .FirstOrDefaultAsync(ct);

            if (ingredientId != Guid.Empty)
            {
                return await LoadIngredientAsync(ingredientId, ct);
            }
        }

        return null;
    }

    public async Task<CanonicalIngredient?> GetByNormalizedAliasAsync(
        string normalizedAlias,
        IReadOnlyList<string> languageCodes,
        CancellationToken ct = default)
    {
        foreach (var language in languageCodes)
        {
            var ingredientId = await dbContext.IngredientTranslationAliases
                .AsNoTracking()
                .Where(x =>
                    x.NormalizedAlias == normalizedAlias
                    && x.Translation.LanguageCode == language)
                .Select(x => x.Translation.IngredientId)
                .FirstOrDefaultAsync(ct);

            if (ingredientId != Guid.Empty)
            {
                return await LoadIngredientAsync(ingredientId, ct);
            }
        }

        return null;
    }

    public async Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(
        string normalizedQuery,
        IReadOnlyList<string> languageCodes,
        int take,
        CancellationToken ct = default)
    {
        var languageSet = languageCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var query = dbContext.Ingredients
            .AsNoTracking()
            .Include(x => x.Translations)
            .ThenInclude(t => t.Aliases)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            var tokens = SplitQueryTokens(normalizedQuery);
            query = query.Where(x => x.Translations.Any(t =>
                languageSet.Contains(t.LanguageCode)
                && (t.NormalizedDisplayName.Contains(normalizedQuery)
                    || tokens.Any(token => t.NormalizedDisplayName.Contains(token))
                    || t.Aliases.Any(a =>
                        a.NormalizedAlias.Contains(normalizedQuery)
                        || tokens.Any(token => a.NormalizedAlias.Contains(token))))));

            // Order after loading all matches — Take before OrderBy dropped prefix hits (e.g. "gehakt" for q=ge).
            var matched = await query.ToListAsync(ct);
            return matched
                .OrderByDescending(x =>
                    IngredientDisplayResolver.ResolveNormalizedDisplayName(x, languageCodes)
                        ?.StartsWith(normalizedQuery, StringComparison.Ordinal) == true)
                .ThenBy(x => IngredientDisplayResolver.Resolve(x, languageCodes).DisplayName, StringComparer.OrdinalIgnoreCase)
                .Take(take)
                .ToList();
        }

        var items = await query.ToListAsync(ct);
        return items
            .OrderBy(x => IngredientDisplayResolver.Resolve(x, languageCodes).DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    public async Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(
        string normalizedQuery,
        IReadOnlyList<string> languageCodes,
        int take,
        CancellationToken ct = default)
    {
        var languageSet = languageCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tokens = SplitQueryTokens(normalizedQuery);
        var matched = await dbContext.Ingredients
            .AsNoTracking()
            .Include(x => x.Translations)
            .ThenInclude(t => t.Aliases)
            .Where(x => x.Translations.Any(t =>
                languageSet.Contains(t.LanguageCode)
                && (t.NormalizedDisplayName.Contains(normalizedQuery)
                    || normalizedQuery.Contains(t.NormalizedDisplayName)
                    || tokens.Any(token => t.NormalizedDisplayName.Contains(token))
                    || t.Aliases.Any(a =>
                        a.NormalizedAlias.Contains(normalizedQuery)
                        || normalizedQuery.Contains(a.NormalizedAlias)
                        || tokens.Any(token => a.NormalizedAlias.Contains(token))))))
            .ToListAsync(ct);

        return matched
            .OrderBy(x => IngredientDisplayResolver.Resolve(x, languageCodes).DisplayName, StringComparer.OrdinalIgnoreCase)
            .Take(take)
            .ToList();
    }

    public async Task<CanonicalIngredient> FindOrCreateAsync(
        string languageCode,
        string displayName,
        string normalizedDisplayName,
        string? alias,
        string? normalizedAlias,
        CancellationToken ct = default)
    {
        var language = IngredientLanguageFallback.ResolveStorageLanguageCode(languageCode);
        if (language.Length == 0)
        {
            throw new ArgumentException("Language code is required.", nameof(languageCode));
        }

        // SqlServerRetryingExecutionStrategy forbids user-initiated transactions unless the whole
        // unit runs via CreateExecutionStrategy (same pattern as EfUnitOfWork).
        // SQLite (unit tests) still re-queries after DbUpdateException for race safety.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);

            try
            {
                var existing = await FindByNormalizedDisplayNameTrackedAsync(language, normalizedDisplayName, ct);
                if (existing is not null)
                {
                    await transaction.CommitAsync(ct);
                    return existing;
                }

                if (!string.IsNullOrWhiteSpace(normalizedAlias))
                {
                    var viaAlias = await FindByNormalizedAliasInLanguageTrackedAsync(language, normalizedAlias, ct);
                    if (viaAlias is not null)
                    {
                        await transaction.CommitAsync(ct);
                        return viaAlias;
                    }
                }

                var ingredient = new CanonicalIngredient
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTimeOffset.UtcNow,
                };

                var translation = new IngredientTranslation
                {
                    Id = Guid.NewGuid(),
                    IngredientId = ingredient.Id,
                    LanguageCode = language,
                    DisplayName = displayName,
                    NormalizedDisplayName = normalizedDisplayName,
                };

                await dbContext.Ingredients.AddAsync(ingredient, ct);
                await dbContext.IngredientTranslations.AddAsync(translation, ct);

                if (!string.IsNullOrWhiteSpace(alias)
                    && !string.IsNullOrWhiteSpace(normalizedAlias)
                    && !string.Equals(normalizedAlias, normalizedDisplayName, StringComparison.Ordinal))
                {
                    await dbContext.IngredientTranslationAliases.AddAsync(new IngredientTranslationAlias
                    {
                        Id = Guid.NewGuid(),
                        IngredientTranslationId = translation.Id,
                        Alias = alias,
                        NormalizedAlias = normalizedAlias,
                    }, ct);
                }

                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);

                ingredient.Translations.Add(translation);
                return ingredient;
            }
            catch (DbUpdateException)
            {
                await transaction.RollbackAsync(ct);
                dbContext.ChangeTracker.Clear();

                var raced = await FindByNormalizedDisplayNameTrackedAsync(language, normalizedDisplayName, ct)
                    ?? (!string.IsNullOrWhiteSpace(normalizedAlias)
                        ? await FindByNormalizedAliasInLanguageTrackedAsync(language, normalizedAlias, ct)
                        : null);

                if (raced is not null)
                {
                    return raced;
                }

                throw;
            }
        });
    }

    public async Task AddMatchLogAsync(IngredientMatchLog log, CancellationToken ct = default)
    {
        await dbContext.IngredientMatchLogs.AddAsync(log, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Tag>> SearchTagsAsync(string normalizedQuery, int take, CancellationToken ct = default)
    {
        return await dbContext.Tags
            .AsNoTracking()
            .Where(x => x.NormalizedName.Contains(normalizedQuery))
            .OrderBy(x => x.Name)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task AddTagsAsync(Guid ingredientId, IReadOnlyList<(string Name, string NormalizedName)> tags, CancellationToken ct = default)
    {
        var existing = await dbContext.Tags
            .Where(x => tags.Select(t => t.NormalizedName).Contains(x.NormalizedName))
            .ToDictionaryAsync(x => x.NormalizedName, ct);

        foreach (var tag in tags)
        {
            if (!existing.TryGetValue(tag.NormalizedName, out var entity))
            {
                entity = new Tag
                {
                    Id = Guid.NewGuid(),
                    Name = tag.Name,
                    NormalizedName = tag.NormalizedName
                };
                existing[tag.NormalizedName] = entity;
                await dbContext.Tags.AddAsync(entity, ct);
            }

            var exists = await dbContext.IngredientTags.AnyAsync(
                x => x.IngredientId == ingredientId && x.TagId == entity.Id,
                ct);

            if (!exists)
            {
                await dbContext.IngredientTags.AddAsync(new IngredientTag
                {
                    IngredientId = ingredientId,
                    TagId = entity.Id
                }, ct);
            }
        }

        await dbContext.SaveChangesAsync(ct);
    }

    private Task<CanonicalIngredient?> LoadIngredientAsync(Guid ingredientId, CancellationToken ct) =>
        dbContext.Ingredients
            .AsNoTracking()
            .Include(x => x.Translations)
            .ThenInclude(t => t.Aliases)
            .FirstOrDefaultAsync(x => x.Id == ingredientId, ct);

    private async Task<CanonicalIngredient?> FindByNormalizedDisplayNameTrackedAsync(
        string language,
        string normalizedDisplayName,
        CancellationToken ct)
    {
        var ingredientId = await dbContext.IngredientTranslations
            .Where(x => x.LanguageCode == language && x.NormalizedDisplayName == normalizedDisplayName)
            .Select(x => x.IngredientId)
            .FirstOrDefaultAsync(ct);

        return ingredientId == Guid.Empty ? null : await LoadIngredientAsync(ingredientId, ct);
    }

    private async Task<CanonicalIngredient?> FindByNormalizedAliasInLanguageTrackedAsync(
        string language,
        string normalizedAlias,
        CancellationToken ct)
    {
        var ingredientId = await dbContext.IngredientTranslationAliases
            .Where(x => x.NormalizedAlias == normalizedAlias && x.Translation.LanguageCode == language)
            .Select(x => x.Translation.IngredientId)
            .FirstOrDefaultAsync(ct);

        return ingredientId == Guid.Empty ? null : await LoadIngredientAsync(ingredientId, ct);
    }

    private static string[] SplitQueryTokens(string normalizedQuery) =>
        normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
