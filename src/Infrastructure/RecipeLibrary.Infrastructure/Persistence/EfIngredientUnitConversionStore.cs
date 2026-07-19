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

    public async Task<IReadOnlyList<IngredientUnitConversion>> GetConversionsForIngredientsAsync(
        IReadOnlyCollection<Guid> canonicalIngredientIds,
        IReadOnlyCollection<Unit> fromUnits,
        Unit toUnit,
        CancellationToken ct = default)
    {
        if (canonicalIngredientIds.Count == 0 || fromUnits.Count == 0)
        {
            return [];
        }

        return await dbContext.IngredientUnitConversions
            .AsNoTracking()
            .Include(x => x.ConversionSource)
            .Where(x =>
                canonicalIngredientIds.Contains(x.CanonicalIngredientId)
                && fromUnits.Contains(x.FromUnit)
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
        var normalizedName = NormalizeDisplayName(displayName);
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

    public async Task<IReadOnlyList<IngredientUnitConversionSuggestion>> GetPendingSuggestionsBatchAsync(
        IReadOnlyCollection<Guid> canonicalIngredientIds,
        IReadOnlyCollection<string> displayNamesWithoutCanonical,
        IReadOnlyCollection<Unit> fromUnits,
        Unit toUnit,
        CancellationToken ct = default)
    {
        if (fromUnits.Count == 0
            || (canonicalIngredientIds.Count == 0 && displayNamesWithoutCanonical.Count == 0))
        {
            return [];
        }

        var normalizedNames = displayNamesWithoutCanonical
            .Select(NormalizeDisplayName)
            .Where(static n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var query = dbContext.IngredientUnitConversionSuggestions
            .AsNoTracking()
            .Where(x =>
                x.Status == ConversionSuggestionStatus.Pending
                && fromUnits.Contains(x.FromUnit)
                && x.ToUnit == toUnit);

        if (canonicalIngredientIds.Count > 0 && normalizedNames.Count > 0)
        {
            query = query.Where(x =>
                (x.CanonicalIngredientId != null && canonicalIngredientIds.Contains(x.CanonicalIngredientId.Value))
                || (x.CanonicalIngredientId == null && normalizedNames.Contains(x.IngredientDisplayName)));
        }
        else if (canonicalIngredientIds.Count > 0)
        {
            query = query.Where(x =>
                x.CanonicalIngredientId != null
                && canonicalIngredientIds.Contains(x.CanonicalIngredientId.Value));
        }
        else
        {
            query = query.Where(x =>
                x.CanonicalIngredientId == null
                && normalizedNames.Contains(x.IngredientDisplayName));
        }

        return await query.ToListAsync(ct);
    }

    public async Task<IngredientUnitConversionSuggestion> AddOrGetPendingSuggestionAsync(
        IngredientUnitConversionSuggestion suggestion,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        suggestion.IngredientDisplayName = NormalizeDisplayName(suggestion.IngredientDisplayName);

        var existing = await GetPendingSuggestionAsync(
            suggestion.CanonicalIngredientId,
            suggestion.IngredientDisplayName,
            suggestion.FromUnit,
            suggestion.ToUnit,
            ct);
        if (existing is not null)
        {
            return existing;
        }

        try
        {
            dbContext.IngredientUnitConversionSuggestions.Add(suggestion);
            await dbContext.SaveChangesAsync(ct);
            return suggestion;
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(suggestion).State = EntityState.Detached;
            var raced = await GetPendingSuggestionAsync(
                suggestion.CanonicalIngredientId,
                suggestion.IngredientDisplayName,
                suggestion.FromUnit,
                suggestion.ToUnit,
                ct);
            if (raced is not null)
            {
                return raced;
            }

            throw;
        }
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

    internal static string NormalizeDisplayName(string displayName) =>
        (displayName ?? string.Empty).Trim().ToLowerInvariant();
}
