using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Abstractions;

/// <summary>Access checks for shopping-list groups, lists, and items.</summary>
public interface IShoppingListAccess
{
    Task<bool> IsGroupAccessibleAsync(Guid groupId, string? ownerUserId, CancellationToken ct = default);

    Task<bool> IsListAccessibleAsync(Guid listId, string? ownerUserId, CancellationToken ct = default);

    Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default);
}

/// <summary>Read shopping-list groups and lists.</summary>
public interface IShoppingListQueries
{
    Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default);

    Task<ShoppingListGroup?> GetGroupByOwnerUserIdAsync(string ownerUserId, CancellationToken ct = default);

    Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default);

    Task<ShoppingList?> GetPrimaryListInGroupAsync(Guid groupId, CancellationToken ct = default);

    Task<bool> GroupHasSecondListAsync(Guid groupId, CancellationToken ct = default);

    Task<int> GetUncheckedItemCountForGroupAsync(Guid groupId, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default);
}

/// <summary>
/// Mutations for shopping lists and items.
/// <para>
/// Persistence style (hybrid with <see cref="IUnitOfWork"/>):
/// </para>
/// <list type="bullet">
/// <item>
/// <description>
/// Self-contained (own transaction / SaveChanges): <see cref="CreateGroupWithPrimaryListAsync"/>
/// (when <c>ownerUserId</c> is set), <see cref="ReplaceListItemsAsync"/>.
/// </description>
/// </item>
/// <item>
/// <description>
/// Immediate <c>ExecuteDelete</c>/<c>ExecuteUpdate</c> (auto-commit outside an ambient transaction):
/// clear/delete/toggle/remove/rename/quantity helpers.
/// </description>
/// </item>
/// <item>
/// <description>
/// Tracked only — caller must <see cref="IUnitOfWork.SaveChangesAsync"/>:
/// <see cref="AddListToGroupAsync"/>.
/// </description>
/// </item>
/// </list>
/// </summary>
public interface IShoppingListCommands
{
    /// <summary>
    /// Creates a group with a primary list. When <paramref name="ownerUserId"/> is set,
    /// persists immediately (race-safe unique owner index). Otherwise tracks until UoW save.
    /// </summary>
    Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(
        string primaryListName,
        string? ownerUserId = null,
        CancellationToken ct = default);

    Task ClearListItemsAsync(Guid shoppingListId, CancellationToken ct = default);

    Task DeleteListAsync(Guid shoppingListId, CancellationToken ct = default);

    Task DeleteGroupAsync(Guid groupId, CancellationToken ct = default);

    /// <summary>
    /// Replaces all items on the list in a self-contained transaction.
    /// When <paramref name="expectedUpdatedAt"/> is set,
    /// fails with <see cref="InvalidOperationException"/> if the list changed concurrently.
    /// </summary>
    Task ReplaceListItemsAsync(
        Guid shoppingListId,
        IReadOnlyList<ShoppingListItem> items,
        DateTimeOffset? expectedUpdatedAt = null,
        CancellationToken ct = default);

    /// <summary>Tracks a new list; caller must <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    Task<ShoppingList> AddListToGroupAsync(Guid groupId, string name, int storeOrder, CancellationToken ct = default);

    Task<bool> ToggleItemCheckedAsync(Guid itemId, bool isChecked, CancellationToken ct = default);

    Task<bool> RemoveItemAsync(Guid itemId, CancellationToken ct = default);

    Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default);

    Task<bool> UpdateItemQuantityAsync(Guid itemId, decimal quantity, CancellationToken ct = default);
}

/// <summary>
/// Combined shopping-list persistence port. Prefer injecting the segregated interfaces
/// (<see cref="IShoppingListAccess"/>, <see cref="IShoppingListQueries"/>, <see cref="IShoppingListCommands"/>)
/// when a client only needs a subset.
/// </summary>
public interface IShoppingListRepository : IShoppingListAccess, IShoppingListQueries, IShoppingListCommands;
