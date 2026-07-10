using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Application.ShoppingLists;

internal static class ShoppingListAccessGuard
{
    public static async Task EnsureGroupAccessAsync(
        IShoppingListRepository repository,
        Guid groupId,
        string? ownerUserId,
        CancellationToken ct)
    {
        if (ownerUserId is null)
        {
            return;
        }

        if (!await repository.IsGroupAccessibleAsync(groupId, ownerUserId, ct))
        {
            throw new UnauthorizedAccessException("Shopping list group is not accessible.");
        }
    }

    public static async Task EnsureListAccessAsync(
        IShoppingListRepository repository,
        Guid listId,
        string? ownerUserId,
        CancellationToken ct)
    {
        if (ownerUserId is null)
        {
            return;
        }

        if (!await repository.IsListAccessibleAsync(listId, ownerUserId, ct))
        {
            throw new UnauthorizedAccessException("Shopping list is not accessible.");
        }
    }

    public static async Task EnsureItemAccessAsync(
        IShoppingListRepository repository,
        Guid itemId,
        string? ownerUserId,
        CancellationToken ct)
    {
        if (ownerUserId is null)
        {
            return;
        }

        var item = await repository.GetItemByIdAsync(itemId, ct)
            ?? throw new InvalidOperationException("Shopping list item not found.");

        await EnsureListAccessAsync(repository, item.ShoppingListId, ownerUserId, ct);
    }
}
