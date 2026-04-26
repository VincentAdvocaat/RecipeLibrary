using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class EfIngredientRepository(RecipeDbContext dbContext) : IIngredientRepository
{
    public Task<CanonicalIngredient?> GetByNormalizedNameAsync(string normalizedName, CancellationToken ct = default)
    {
        return dbContext.Ingredients
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.NormalizedName == normalizedName, ct);
    }

    public Task<CanonicalIngredient?> GetByNormalizedAliasAsync(string normalizedAlias, CancellationToken ct = default)
    {
        return dbContext.IngredientAliases
            .AsNoTracking()
            .Where(x => x.NormalizedAlias == normalizedAlias)
            .Select(x => x.Ingredient)
            .SingleOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CanonicalIngredient>> SearchAsync(string normalizedQuery, int take, CancellationToken ct = default)
    {
        var query = dbContext.Ingredients.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalizedQuery))
        {
            query = query.Where(x => x.NormalizedName.Contains(normalizedQuery));
            return await query
                .OrderByDescending(x => x.NormalizedName.StartsWith(normalizedQuery))
                .ThenBy(x => x.CanonicalName)
                .Take(take)
                .ToListAsync(ct);
        }

        return await query
            .OrderBy(x => x.CanonicalName)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CanonicalIngredient>> GetFuzzyCandidatesAsync(string normalizedQuery, int take, CancellationToken ct = default)
    {
        return await dbContext.Ingredients
            .AsNoTracking()
            .Where(x => x.NormalizedName.Contains(normalizedQuery) || normalizedQuery.Contains(x.NormalizedName))
            .OrderBy(x => x.CanonicalName)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<CanonicalIngredient> CreateIngredientWithAliasAsync(
        string canonicalName,
        string normalizedName,
        string alias,
        string normalizedAlias,
        CancellationToken ct = default)
    {
        var ingredient = new CanonicalIngredient
        {
            Id = Guid.NewGuid(),
            CanonicalName = canonicalName,
            NormalizedName = normalizedName,
            CreatedAt = DateTimeOffset.UtcNow
        };

        var aliasEntity = new IngredientAlias
        {
            Id = Guid.NewGuid(),
            IngredientId = ingredient.Id,
            Alias = alias,
            NormalizedAlias = normalizedAlias
        };

        await dbContext.Ingredients.AddAsync(ingredient, ct);
        await dbContext.IngredientAliases.AddAsync(aliasEntity, ct);
        await dbContext.SaveChangesAsync(ct);
        return ingredient;
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
}
