using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class EfShoppingListRepository(RecipeDbContext dbContext) : IShoppingListRepository
{
    public Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default)
    {
        return dbContext.ShoppingListGroups
            .AsNoTracking()
            .Include(g => g.Lists.OrderBy(l => l.StoreOrder))
            .ThenInclude(l => l.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.DisplayName))
            .ThenInclude(i => i.Sources)
            .FirstOrDefaultAsync(g => g.Id == groupId, ct);
    }

    public async Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(
        string primaryListName,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var groupId = Guid.NewGuid();
        var listId = Guid.NewGuid();

        var group = new ShoppingListGroup
        {
            Id = groupId,
            CreatedAt = now,
            UpdatedAt = now,
            Lists =
            [
                new ShoppingList
                {
                    Id = listId,
                    GroupId = groupId,
                    Name = primaryListName.Trim(),
                    StoreOrder = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
            ],
        };

        await dbContext.ShoppingListGroups.AddAsync(group, ct);
        await dbContext.SaveChangesAsync(ct);
        return group;
    }

    public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default)
    {
        return dbContext.ShoppingLists
            .AsNoTracking()
            .Include(l => l.Items)
            .ThenInclude(i => i.Sources)
            .FirstOrDefaultAsync(l => l.Id == listId, ct);
    }

    public Task<ShoppingList?> GetPrimaryListInGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        return dbContext.ShoppingLists
            .AsNoTracking()
            .Include(l => l.Items)
            .ThenInclude(i => i.Sources)
            .FirstOrDefaultAsync(l => l.GroupId == groupId && l.StoreOrder == 1, ct);
    }

    public Task<bool> GroupHasSecondListAsync(Guid groupId, CancellationToken ct = default)
    {
        return dbContext.ShoppingLists
            .AnyAsync(l => l.GroupId == groupId && l.StoreOrder == 2, ct);
    }

    public Task<int> GetUncheckedItemCountForGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        return dbContext.ShoppingListItems
            .Where(i => i.ShoppingList!.GroupId == groupId && !i.IsChecked)
            .CountAsync(ct);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        dbContext.SaveChangesAsync(ct);

    public async Task ClearListItemsAsync(Guid shoppingListId, CancellationToken ct = default)
    {
        await dbContext.ShoppingListItemSources
            .Where(s => s.Item!.ShoppingListId == shoppingListId)
            .ExecuteDeleteAsync(ct);

        await dbContext.ShoppingListItems
            .Where(i => i.ShoppingListId == shoppingListId)
            .ExecuteDeleteAsync(ct);

        await dbContext.ShoppingLists
            .Where(l => l.Id == shoppingListId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(l => l.UpdatedAt, DateTimeOffset.UtcNow),
                ct);
    }

    public async Task DeleteListAsync(Guid shoppingListId, CancellationToken ct = default)
    {
        var list = await dbContext.ShoppingLists
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == shoppingListId, ct);

        if (list is null)
        {
            return;
        }

        var groupId = list.GroupId;
        var remainingCount = await dbContext.ShoppingLists
            .CountAsync(l => l.GroupId == groupId && l.Id != shoppingListId, ct);

        await dbContext.ShoppingLists
            .Where(l => l.Id == shoppingListId)
            .ExecuteDeleteAsync(ct);

        if (remainingCount == 0)
        {
            await dbContext.ShoppingListGroups
                .Where(g => g.Id == groupId)
                .ExecuteDeleteAsync(ct);
        }
        else
        {
            await dbContext.ShoppingListGroups
                .Where(g => g.Id == groupId)
                .ExecuteUpdateAsync(
                    s => s.SetProperty(g => g.UpdatedAt, DateTimeOffset.UtcNow),
                    ct);
        }
    }

    public async Task DeleteGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        await dbContext.ShoppingListGroups
            .Where(g => g.Id == groupId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task ReplaceListItemsAsync(Guid shoppingListId, IReadOnlyList<ShoppingListItem> items, CancellationToken ct = default)
    {
        await dbContext.ShoppingListItemSources
            .Where(s => s.Item!.ShoppingListId == shoppingListId)
            .ExecuteDeleteAsync(ct);

        await dbContext.ShoppingListItems
            .Where(i => i.ShoppingListId == shoppingListId)
            .ExecuteDeleteAsync(ct);

        foreach (var entry in dbContext.ChangeTracker.Entries<ShoppingListItem>()
            .Where(e => e.Entity.ShoppingListId == shoppingListId)
            .ToList())
        {
            entry.State = EntityState.Detached;
        }

        foreach (var entry in dbContext.ChangeTracker.Entries<ShoppingListItemSource>().ToList())
        {
            entry.State = EntityState.Detached;
        }

        if (items.Count > 0)
        {
            await dbContext.ShoppingListItems.AddRangeAsync(items, ct);
        }

        await dbContext.ShoppingLists
            .Where(l => l.Id == shoppingListId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(l => l.UpdatedAt, DateTimeOffset.UtcNow),
                ct);

        foreach (var entry in dbContext.ChangeTracker.Entries<ShoppingList>().ToList())
        {
            entry.State = EntityState.Detached;
        }

        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<ShoppingList> AddListToGroupAsync(Guid groupId, string name, int storeOrder, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var list = new ShoppingList
        {
            Id = Guid.NewGuid(),
            GroupId = groupId,
            Name = name.Trim(),
            StoreOrder = storeOrder,
            CreatedAt = now,
            UpdatedAt = now,
        };

        await dbContext.ShoppingLists.AddAsync(list, ct);

        await dbContext.ShoppingListGroups
            .Where(g => g.Id == groupId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(g => g.UpdatedAt, now),
                ct);

        await dbContext.SaveChangesAsync(ct);
        return list;
    }

    public async Task<bool> ToggleItemCheckedAsync(Guid itemId, bool isChecked, CancellationToken ct = default)
    {
        var updated = await dbContext.ShoppingListItems
            .Where(i => i.Id == itemId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.IsChecked, isChecked),
                ct);

        return updated > 0;
    }

    public async Task<bool> RemoveItemAsync(Guid itemId, CancellationToken ct = default)
    {
        var item = await dbContext.ShoppingListItems
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);

        if (item is null)
        {
            return false;
        }

        await dbContext.ShoppingListItemSources
            .Where(s => s.ShoppingListItemId == itemId)
            .ExecuteDeleteAsync(ct);

        await dbContext.ShoppingListItems
            .Where(i => i.Id == itemId)
            .ExecuteDeleteAsync(ct);

        await dbContext.ShoppingLists
            .Where(l => l.Id == item.ShoppingListId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(l => l.UpdatedAt, DateTimeOffset.UtcNow),
                ct);

        return true;
    }

    public Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default)
    {
        return dbContext.ShoppingListItems
            .AsNoTracking()
            .Include(i => i.Sources)
            .FirstOrDefaultAsync(i => i.Id == itemId, ct);
    }

    public async Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default)
    {
        var updated = await dbContext.ShoppingLists
            .Where(l => l.Id == shoppingListId)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(l => l.Name, name)
                    .SetProperty(l => l.UpdatedAt, DateTimeOffset.UtcNow),
                ct);

        return updated > 0;
    }

    public async Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default)
    {
        var query = dbContext.ShoppingLists.AsNoTracking();

        if (groupId is Guid scopedGroupId && scopedGroupId != Guid.Empty)
        {
            query = query.Where(l => l.GroupId == scopedGroupId);
        }

        return await query.Select(l => l.Name).ToListAsync(ct);
    }
}
