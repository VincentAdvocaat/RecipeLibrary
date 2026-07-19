using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class RemovePantryItemCommandHandler(
    IPantryRepository repository,
    IShoppingListRepository shoppingListRepository,
    ICurrentUser userContext)
    : ICommandHandler<RemovePantryItemCommand, RemovePantryItemResult>
{
    public async Task<RemovePantryItemResult> HandleAsync(
        RemovePantryItemCommand command,
        CancellationToken ct = default)
    {
        await ShoppingListAccessGuard.EnsureGroupAccessAsync(
            shoppingListRepository,
            command.ShoppingListGroupId,
            userContext.UserId,
            ct);

        var ownerKey = PantryOwnerKey.Resolve(userContext.UserId, command.ShoppingListGroupId);
        var removed = await repository.RemoveAsync(command.ItemId, ownerKey, ct);
        return new RemovePantryItemResult(removed);
    }
}
