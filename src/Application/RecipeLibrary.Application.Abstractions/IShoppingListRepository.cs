using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Abstractions;

public interface IShoppingListRepository
{
    Task<ShoppingListGroup?> GetGroupWithListsAsync(Guid groupId, CancellationToken ct = default);

    Task<ShoppingListGroup> CreateGroupWithPrimaryListAsync(
        string primaryListName,
        CancellationToken ct = default);

    Task<ShoppingList?> GetListByIdAsync(Guid listId, CancellationToken ct = default);

    Task<ShoppingList?> GetPrimaryListInGroupAsync(Guid groupId, CancellationToken ct = default);

    Task<bool> GroupHasSecondListAsync(Guid groupId, CancellationToken ct = default);

    Task<int> GetUncheckedItemCountForGroupAsync(Guid groupId, CancellationToken ct = default);

    Task SaveChangesAsync(CancellationToken ct = default);

    Task ClearListItemsAsync(Guid shoppingListId, CancellationToken ct = default);

    Task DeleteListAsync(Guid shoppingListId, CancellationToken ct = default);

    Task DeleteGroupAsync(Guid groupId, CancellationToken ct = default);

    Task ReplaceListItemsAsync(Guid shoppingListId, IReadOnlyList<ShoppingListItem> items, CancellationToken ct = default);

    Task<ShoppingList> AddListToGroupAsync(Guid groupId, string name, int storeOrder, CancellationToken ct = default);

    Task<bool> ToggleItemCheckedAsync(Guid itemId, bool isChecked, CancellationToken ct = default);

    Task<bool> RemoveItemAsync(Guid itemId, CancellationToken ct = default);

    Task<ShoppingListItem?> GetItemByIdAsync(Guid itemId, CancellationToken ct = default);

    Task<bool> UpdateListNameAsync(Guid shoppingListId, string name, CancellationToken ct = default);

    Task<IReadOnlyList<string>> GetListNamesAsync(Guid? groupId = null, CancellationToken ct = default);
}
