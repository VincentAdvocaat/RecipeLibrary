using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class RemoveShoppingListItemCommandHandler(
    IShoppingListRepository repository,
    IShoppingListUserContext userContext)
    : ICommandHandler<RemoveShoppingListItemCommand, RemoveShoppingListItemResult>
{
    public async Task<RemoveShoppingListItemResult> HandleAsync(
        RemoveShoppingListItemCommand command,
        CancellationToken ct = default)
    {
        await ShoppingListAccessGuard.EnsureItemAccessAsync(
            repository,
            command.ItemId,
            userContext.OwnerUserId,
            ct);

        var removed = await repository.RemoveItemAsync(command.ItemId, ct);
        return new RemoveShoppingListItemResult(removed);
    }
}
