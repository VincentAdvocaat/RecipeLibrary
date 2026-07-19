using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class GetPantryItemsQueryHandler(
    IPantryRepository repository,
    IShoppingListRepository shoppingListRepository,
    ICurrentUser userContext)
    : IQueryHandler<GetPantryItemsQuery, GetPantryItemsResult>
{
    public async Task<GetPantryItemsResult> HandleAsync(GetPantryItemsQuery query, CancellationToken ct = default)
    {
        await ShoppingListAccessGuard.EnsureGroupAccessAsync(
            shoppingListRepository,
            query.ShoppingListGroupId,
            userContext.UserId,
            ct);

        var ownerKey = PantryOwnerKey.Resolve(userContext.UserId, query.ShoppingListGroupId);
        var items = await repository.GetByOwnerKeyAsync(ownerKey, ct);
        return PantryMapping.MapItems(items);
    }
}
