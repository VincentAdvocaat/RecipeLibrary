using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class ClearShoppingListCommandHandler(IShoppingListRepository repository)
    : ICommandHandler<ClearShoppingListCommand, ClearShoppingListResult>
{
    public async Task<ClearShoppingListResult> HandleAsync(
        ClearShoppingListCommand command,
        CancellationToken ct = default)
    {
        var list = await repository.GetListByIdAsync(command.ShoppingListId, ct);
        if (list is null)
        {
            return new ClearShoppingListResult(false);
        }

        await repository.ClearListItemsAsync(command.ShoppingListId, ct);
        return new ClearShoppingListResult(true);
    }
}
