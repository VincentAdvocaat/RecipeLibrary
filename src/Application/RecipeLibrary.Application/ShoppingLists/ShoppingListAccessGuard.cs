using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Application.ShoppingLists;

/// <summary>
/// Enforces shopping-list ownership. Fail-closed: unauthenticated callers (null owner) are denied.
/// Cookie group ids are not a capability when Identity is enabled — only OwnerUserId matches grant access.
/// </summary>
internal static class ShoppingListAccessGuard
{
    public static async Task EnsureGroupAccessAsync(
        IShoppingListAccess repository,
        Guid groupId,
        string? ownerUserId,
        CancellationToken ct)
    {
        RequireAuthenticatedOwner(ownerUserId);

        if (!await repository.IsGroupAccessibleAsync(groupId, ownerUserId, ct))
        {
            throw new UnauthorizedAccessException("Shopping list group is not accessible.");
        }
    }

    public static async Task EnsureListAccessAsync(
        IShoppingListAccess repository,
        Guid listId,
        string? ownerUserId,
        CancellationToken ct)
    {
        RequireAuthenticatedOwner(ownerUserId);

        if (!await repository.IsListAccessibleAsync(listId, ownerUserId, ct))
        {
            throw new UnauthorizedAccessException("Shopping list is not accessible.");
        }
    }

    public static async Task EnsureItemAccessAsync(
        IShoppingListAccess repository,
        Guid itemId,
        string? ownerUserId,
        CancellationToken ct)
    {
        RequireAuthenticatedOwner(ownerUserId);

        var item = await repository.GetItemByIdAsync(itemId, ct)
            ?? throw new InvalidOperationException("Shopping list item not found.");

        await EnsureListAccessAsync(repository, item.ShoppingListId, ownerUserId, ct);
    }

    private static void RequireAuthenticatedOwner(string? ownerUserId)
    {
        if (string.IsNullOrWhiteSpace(ownerUserId))
        {
            throw new UnauthorizedAccessException("Authentication is required to access shopping lists.");
        }
    }
}
