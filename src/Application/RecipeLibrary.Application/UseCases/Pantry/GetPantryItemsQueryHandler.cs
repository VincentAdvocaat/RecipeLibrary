using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class GetPantryItemsQueryHandler(
    IPantryRepository repository,
    IShoppingListRepository shoppingListRepository,
    ICurrentUser userContext,
    IHouseholdRepository? households = null,
    IUnitOfWork? unitOfWork = null)
    : IQueryHandler<GetPantryItemsQuery, GetPantryItemsResult>
{
    public async Task<GetPantryItemsResult> HandleAsync(GetPantryItemsQuery query, CancellationToken ct = default)
    {
        GetPantryItemsResult result = null!;
        async Task ReadAsync(CancellationToken operationCt)
        {
            var ownerKey = await PantryOwnerKey.ResolveForLibraryAsync(shoppingListRepository, households,
                userContext.UserId, query.ShoppingListGroupId, query.HouseholdId, operationCt);
            var items = await repository.GetByOwnerKeyAsync(ownerKey, operationCt);
            result = PantryMapping.MapItems(items);
        }

        if (query.HouseholdId.HasValue)
            await (unitOfWork ?? throw new InvalidOperationException("Household pantry requires a unit of work."))
                .ExecuteInSerializableTransactionAsync(ReadAsync, ct);
        else
            await ReadAsync(ct);
        return result;
    }
}
