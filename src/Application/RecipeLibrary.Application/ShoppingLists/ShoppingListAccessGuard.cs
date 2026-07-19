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
        var userId = RequireUserId(ownerUserId);

        if (!await repository.IsGroupAccessibleAsync(groupId, userId, ct))
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
        var userId = RequireUserId(ownerUserId);

        if (!await repository.IsListAccessibleAsync(listId, userId, ct))
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
        RequireUserId(ownerUserId);

        var item = await repository.GetItemByIdAsync(itemId, ct)
            ?? throw new InvalidOperationException("Shopping list item not found.");

        await EnsureListAccessAsync(repository, item.ShoppingListId, ownerUserId, ct);
    }

    private static string RequireUserId(string? ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        return ownerUserId;
    }
}
