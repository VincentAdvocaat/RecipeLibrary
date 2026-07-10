using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Pantry;

namespace RecipeLibrary.Web.Services;

public sealed class PantrySessionService(
    ShoppingListSessionService shoppingSession,
    IShoppingListUserContext userContext)
{
    public async Task<string> GetOwnerKeyAsync(CancellationToken ct = default)
    {
        var group = await shoppingSession.GetOrCreateGroupAsync(ct);
        return PantryOwnerKey.Resolve(userContext.OwnerUserId, group.GroupId);
    }
}
