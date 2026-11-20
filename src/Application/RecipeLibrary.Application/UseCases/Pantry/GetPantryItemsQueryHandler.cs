using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class GetPantryItemsQueryHandler(
    IPantryRepository repository,
    IShoppingListRepository shoppingListRepository,
    ICurrentUser userContext,
    IHouseholdRepository? households = null)
    : IQueryHandler<GetPantryItemsQuery, GetPantryItemsResult>
{
    public async Task<GetPantryItemsResult> HandleAsync(GetPantryItemsQuery query, CancellationToken ct = default)
    {
        var ownerKey = await PantryOwnerKey.ResolveForLibraryAsync(shoppingListRepository, households,
            userContext.UserId, query.ShoppingListGroupId, query.HouseholdId, ct);
        var items = await repository.GetByOwnerKeyAsync(ownerKey, ct);
        return PantryMapping.MapItems(items);
    }
}
