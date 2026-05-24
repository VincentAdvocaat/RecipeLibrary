using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class RemoveShoppingListItemCommandHandler(IShoppingListRepository repository)
    : ICommandHandler<RemoveShoppingListItemCommand, RemoveShoppingListItemResult>
{
    public async Task<RemoveShoppingListItemResult> HandleAsync(
        RemoveShoppingListItemCommand command,
        CancellationToken ct = default)
    {
        var removed = await repository.RemoveItemAsync(command.ItemId, ct);
        return new RemoveShoppingListItemResult(removed);
    }
}
