using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class RemovePantryItemCommandHandler(
    IPantryRepository repository,
    IShoppingListRepository shoppingListRepository,
    ICurrentUser userContext,
    IUnitOfWork unitOfWork,
    IHouseholdRepository? households = null)
    : ICommandHandler<RemovePantryItemCommand, RemovePantryItemResult>
{
    public async Task<RemovePantryItemResult> HandleAsync(
        RemovePantryItemCommand command,
        CancellationToken ct = default)
    {
        var removed = false;
        async Task RemoveAsync(CancellationToken operationCt)
        {
            var ownerKey = await PantryOwnerKey.ResolveForLibraryAsync(shoppingListRepository, households,
                userContext.UserId, command.ShoppingListGroupId, command.HouseholdId, operationCt);
            removed = await repository.RemoveAsync(command.ItemId, ownerKey, operationCt);
        }

        if (command.HouseholdId.HasValue)
            await unitOfWork.ExecuteInSerializableTransactionAsync(RemoveAsync, ct);
        else
        {
            await RemoveAsync(ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return new RemovePantryItemResult(removed);
    }
}
