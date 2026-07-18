using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Tests;

/// <summary>
/// Configurable shopping-list repository for unit tests.
/// When <see cref="AccessibleByDefault"/> is false, only ids in the allow sets are accessible.
/// </summary>
public sealed class RecordingShoppingListRepository : IShoppingListRepository
{
    public bool AccessibleByDefault { get; init; } = true;

    public HashSet<Guid> AccessibleGroupIds { get; } = [];
    public HashSet<Guid> AccessibleListIds { get; } = [];

    public ShoppingListGroup? Group { get; init; }
    public ShoppingList? List { get; set; }
    public ShoppingListItem? Item { get; init; }
    public int UncheckedItemCount { get; init; }
    public bool ToggleResult { get; init; } = true;
    public bool RemoveResult { get; init; } = true;
    public bool UpdateNameResult { get; init; } = true;
    public bool UpdateQuantityResult { get; init; } = true;

    public Guid? LastClearedListId { get; private set; }
    public Guid? LastDeletedListId { get; private set; }
    public Guid? LastDeletedGroupId { get; private set; }
    public Guid? LastToggledItemId { get; private set; }
    public bool? LastToggleChecked { get; private set; }
    public Guid? LastRemovedItemId { get; private set; }
    public Guid? LastUpdatedNameListId { get; private set; }
    public string? LastUpdatedName { get; private set; }
    public Guid? LastReplacedListId { get; private set; }
    public IReadOnlyList<ShoppingListItem>? LastReplacedItems { get; private set; }
    public Guid? LastQuantityItemId { get; private set; }
    public decimal? LastQuantity { get; private set; }

    public Task<bool> IsGroupAccessibleAsync(Guid groupId, string? ownerUserId, CancellationToken ct = default) =>
        Task.FromResult(AccessibleByDefault || AccessibleGroupIds.Contains(groupId));

    public Task<bool> IsListAccessibleAsync(Guid listId, string? ownerUserId, CancellationToken ct = default) =>
        Task.FromResult(AccessibleByDefault || AccessibleListIds.Contains(listId));

    public Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default) =>
        Task.FromResult(Group?.Id == groupId ? Group : null);

    public Task<ShoppingListGroup?> GetGroupByOwnerUserIdAsync(string ownerUserId, CancellationToken ct = default) =>
        Task.FromResult(
            Group is not null && string.Equals(Group.OwnerUserId, ownerUserId, StringComparison.Ordinal)
                ? Group
                : null);

    public Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default) =>
        Task.FromResult(List?.Id == listId ? List : null);

    public Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default) =>
        Task.FromResult(Item?.Id == itemId ? Item : null);

    public Task<int> GetUncheckedItemCountForGroupAsync(Guid groupId, CancellationToken ct = default) =>
        Task.FromResult(UncheckedItemCount);

    public Task ClearListItemsAsync(Guid shoppingListId, CancellationToken ct = default)
    {
        LastClearedListId = shoppingListId;
        return Task.CompletedTask;
    }

    public Task DeleteListAsync(Guid shoppingListId, CancellationToken ct = default)
    {
        LastDeletedListId = shoppingListId;
        return Task.CompletedTask;
    }

    public Task DeleteGroupAsync(Guid groupId, CancellationToken ct = default)
    {
        LastDeletedGroupId = groupId;
        return Task.CompletedTask;
    }

    public Task<bool> ToggleItemCheckedAsync(Guid itemId, bool isChecked, CancellationToken ct = default)
    {
        LastToggledItemId = itemId;
        LastToggleChecked = isChecked;
        return Task.FromResult(ToggleResult);
    }

    public Task<bool> RemoveItemAsync(Guid itemId, CancellationToken ct = default)
    {
        LastRemovedItemId = itemId;
        return Task.FromResult(RemoveResult);
    }

    public Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default)
    {
        LastUpdatedNameListId = shoppingListId;
        LastUpdatedName = name;
        return Task.FromResult(UpdateNameResult);
    }

    public Task ReplaceListItemsAsync(Guid shoppingListId, IReadOnlyList<ShoppingListItem> items, CancellationToken ct = default)
    {
        LastReplacedListId = shoppingListId;
        LastReplacedItems = items;
        if (List?.Id == shoppingListId)
        {
            List.Items = items.ToList();
        }

        return Task.CompletedTask;
    }

    public Task<bool> UpdateItemQuantityAsync(Guid itemId, decimal quantity, CancellationToken ct = default)
    {
        LastQuantityItemId = itemId;
        LastQuantity = quantity;
        return Task.FromResult(UpdateQuantityResult);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;

    public Task<ShoppingList?> GetPrimaryListInGroupAsync(Guid groupId, CancellationToken ct = default) =>
        Task.FromResult(List?.GroupId == groupId ? List : null);

    public Task<bool> GroupHasSecondListAsync(Guid groupId, CancellationToken ct = default) => Task.FromResult(false);

    public Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<string>>([]);

    public Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(
        string primaryListName,
        string? ownerUserId = null,
        CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<ShoppingList> AddListToGroupAsync(
        Guid groupId,
        string name,
        int storeOrder,
        CancellationToken ct = default) =>
        throw new NotImplementedException();
}
