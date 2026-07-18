using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class EfIngredientUnitConversionStore(RecipeDbContext dbContext) : IIngredientUnitConversionStore
{
    public async Task<IReadOnlyList<IngredientUnitConversion>> GetConversionsAsync(
        Guid canonicalIngredientId,
        Unit fromUnit,
        Unit toUnit,
        CancellationToken ct = default)
    {
        return await dbContext.IngredientUnitConversions
            .AsNoTracking()
            .Include(x => x.ConversionSource)
            .Where(x =>
                x.CanonicalIngredientId == canonicalIngredientId
                && x.FromUnit == fromUnit
                && x.ToUnit == toUnit)
            .ToListAsync(ct);
    }

    public async Task<IngredientUnitConversionSuggestion?> GetPendingSuggestionAsync(
        Guid? canonicalIngredientId,
        string displayName,
        Unit fromUnit,
        Unit toUnit,
        CancellationToken ct = default)
    {
        var normalizedName = displayName.Trim();
        var query = dbContext.IngredientUnitConversionSuggestions
            .AsNoTracking()
            .Where(x =>
                x.Status == ConversionSuggestionStatus.Pending
                && x.FromUnit == fromUnit
                && x.ToUnit == toUnit);

        if (canonicalIngredientId is Guid id)
        {
            query = query.Where(x => x.CanonicalIngredientId == id);
        }
        else
        {
            query = query.Where(x =>
                x.CanonicalIngredientId == null
                && x.IngredientDisplayName == normalizedName);
        }

        return await query
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddSuggestionAsync(IngredientUnitConversionSuggestion suggestion, CancellationToken ct = default)
    {
        dbContext.IngredientUnitConversionSuggestions.Add(suggestion);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task AddConversionAsync(IngredientUnitConversion conversion, CancellationToken ct = default)
    {
        dbContext.IngredientUnitConversions.Add(conversion);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task MarkSuggestionAcceptedAsync(Guid suggestionId, CancellationToken ct = default)
    {
        var entity = await dbContext.IngredientUnitConversionSuggestions
            .FirstOrDefaultAsync(x => x.Id == suggestionId, ct);
        if (entity is null)
        {
            return;
        }

        entity.Status = ConversionSuggestionStatus.Accepted;
        await dbContext.SaveChangesAsync(ct);
    }

    public Task<ConversionSource?> GetSourceByNameAsync(string name, CancellationToken ct = default) =>
        dbContext.ConversionSources
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Name == name, ct);

    public async Task<Guid?> FindCanonicalIngredientIdByCatalogKeyAsync(string catalogKey, CancellationToken ct = default)
    {
        var id = await dbContext.Ingredients
            .AsNoTracking()
            .Where(x => x.CatalogKey == catalogKey)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefaultAsync(ct);
        return id;
    }
}
