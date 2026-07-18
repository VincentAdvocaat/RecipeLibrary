using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Tests;

public sealed class RecordingPantryRepository : IPantryRepository
{
    public List<PantryItem> Items { get; set; } = [];

    public PantryItem? UpsertedItem { get; private set; }
    public Guid? LastRemovedItemId { get; private set; }
    public string? LastRemovedOwnerKey { get; private set; }
    public string? LastQueriedOwnerKey { get; private set; }

    public Task<IReadOnlyList<PantryItem>> GetByOwnerKeyAsync(string ownerKey, CancellationToken ct = default)
    {
        LastQueriedOwnerKey = ownerKey;
        return Task.FromResult<IReadOnlyList<PantryItem>>(
            Items.Where(i => i.OwnerUserId == ownerKey).ToList());
    }

    public Task<PantryItem?> GetByIdForOwnerAsync(Guid itemId, string ownerKey, CancellationToken ct = default) =>
        Task.FromResult(Items.FirstOrDefault(i => i.Id == itemId && i.OwnerUserId == ownerKey));

    public Task<PantryItem> UpsertAsync(PantryItem item, CancellationToken ct = default)
    {
        UpsertedItem = item;
        var index = Items.FindIndex(i => i.Id == item.Id);
        if (index >= 0)
        {
            Items[index] = item;
        }
        else
        {
            Items.Add(item);
        }

        return Task.FromResult(item);
    }

    public Task<bool> RemoveAsync(Guid itemId, string ownerKey, CancellationToken ct = default)
    {
        LastRemovedItemId = itemId;
        LastRemovedOwnerKey = ownerKey;
        var removed = Items.RemoveAll(i => i.Id == itemId && i.OwnerUserId == ownerKey) > 0;
        return Task.FromResult(removed);
    }
}
