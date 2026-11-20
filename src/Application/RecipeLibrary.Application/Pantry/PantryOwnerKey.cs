using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.Pantry;

public static class PantryOwnerKey
{
    public static string ForHousehold(Guid id) => $"household:{id:D}";

    public static async Task<string> ResolveForLibraryAsync(
        IShoppingListRepository shoppingLists, IHouseholdRepository? households,
        string? userId, Guid shoppingListGroupId, Guid? householdId, CancellationToken ct)
    {
        if (householdId is { } id)
        {
            if (userId is null || households is null || !await households.IsMemberAsync(userId, id, ct))
            {
                throw new UnauthorizedAccessException("Household access is required.");
            }

            return ForHousehold(id);
        }

        await ShoppingListAccessGuard.EnsureGroupAccessAsync(shoppingLists, shoppingListGroupId, userId, ct);
        return Resolve(userId, shoppingListGroupId);
    }

    public static string Resolve(string? ownerUserId, Guid shoppingListGroupId) =>
        !string.IsNullOrWhiteSpace(ownerUserId)
            ? ownerUserId
            : $"group:{shoppingListGroupId:D}";

    public static void Validate(string ownerKey)
    {
        if (string.IsNullOrWhiteSpace(ownerKey))
        {
            throw new ArgumentException("Pantry owner key is required.");
        }

        if (ownerKey.Length > 256)
        {
            throw new ArgumentException("Pantry owner key must be at most 256 characters.");
        }
    }
}
