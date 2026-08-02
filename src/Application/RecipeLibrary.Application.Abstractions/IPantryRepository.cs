using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Abstractions;

/// <summary>
/// Pantry persistence. <see cref="UpsertAsync"/> tracks until
/// <see cref="IUnitOfWork.SaveChangesAsync"/>; <see cref="RemoveAsync"/> uses immediate ExecuteDelete.
/// </summary>
public interface IPantryRepository
{
    Task<IReadOnlyList<PantryItem>> GetByOwnerKeyAsync(string ownerKey, CancellationToken ct = default);

    Task<PantryItem?> GetByIdForOwnerAsync(Guid itemId, string ownerKey, CancellationToken ct = default);

    /// <summary>Tracks upsert; caller must <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    Task<PantryItem> UpsertAsync(PantryItem item, CancellationToken ct = default);

    /// <summary>Deletes immediately via ExecuteDelete.</summary>
    Task<bool> RemoveAsync(Guid itemId, string ownerKey, CancellationToken ct = default);
}
