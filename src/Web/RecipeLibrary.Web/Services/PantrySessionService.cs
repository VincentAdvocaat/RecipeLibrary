using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Web.Services;

public sealed class PantrySessionService(ShoppingListSessionService shoppingSession)
{
    public async Task<Guid> GetGroupIdAsync(CancellationToken ct = default)
    {
        var group = await shoppingSession.GetOrCreateGroupAsync(ct);
        return group.GroupId;
    }
}
