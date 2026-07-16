using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Abstractions;

public interface IPantryRepository
{
    Task<IReadOnlyList<PantryItem>> GetByOwnerKeyAsync(string ownerKey, CancellationToken ct = default);

    Task<PantryItem?> GetByIdForOwnerAsync(Guid itemId, string ownerKey, CancellationToken ct = default);

    Task<PantryItem> UpsertAsync(PantryItem item, CancellationToken ct = default);

    Task<bool> RemoveAsync(Guid itemId, string ownerKey, CancellationToken ct = default);
}
