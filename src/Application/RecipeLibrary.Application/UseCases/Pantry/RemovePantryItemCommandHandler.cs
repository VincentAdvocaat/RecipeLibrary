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
        var ownerKey = await PantryOwnerKey.ResolveForLibraryAsync(shoppingListRepository, households,
            userContext.UserId, command.ShoppingListGroupId, command.HouseholdId, ct);
        var removed = await repository.RemoveAsync(command.ItemId, ownerKey, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return new RemovePantryItemResult(removed);
    }
}
