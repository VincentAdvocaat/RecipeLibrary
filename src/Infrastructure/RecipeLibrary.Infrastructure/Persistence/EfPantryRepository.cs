using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class EfPantryRepository(RecipeDbContext dbContext) : IPantryRepository
{
    public async Task<IReadOnlyList<PantryItem>> GetByOwnerKeyAsync(string ownerKey, CancellationToken ct = default)
    {
        return await dbContext.PantryItems
            .AsNoTracking()
            .Where(p => p.OwnerUserId == ownerKey)
            .OrderBy(p => p.DisplayName)
            .ToListAsync(ct);
    }

    public Task<PantryItem?> GetByIdForOwnerAsync(Guid itemId, string ownerKey, CancellationToken ct = default)
    {
        return dbContext.PantryItems
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == itemId && p.OwnerUserId == ownerKey, ct);
    }

    public async Task<PantryItem> UpsertAsync(PantryItem item, CancellationToken ct = default)
    {
        var tracked = await dbContext.PantryItems
            .FirstOrDefaultAsync(p => p.Id == item.Id && p.OwnerUserId == item.OwnerUserId, ct);

        if (tracked is not null)
        {
            tracked.CanonicalIngredientId = item.CanonicalIngredientId;
            tracked.DisplayName = item.DisplayName;
            tracked.UpdatedAt = item.UpdatedAt;
            await dbContext.SaveChangesAsync(ct);
            return tracked;
        }

        await dbContext.PantryItems.AddAsync(item, ct);
        await dbContext.SaveChangesAsync(ct);
        return item;
    }

    public async Task<bool> RemoveAsync(Guid itemId, string ownerKey, CancellationToken ct = default)
    {
        var deleted = await dbContext.PantryItems
            .Where(p => p.Id == itemId && p.OwnerUserId == ownerKey)
            .ExecuteDeleteAsync(ct);

        return deleted > 0;
    }
}
