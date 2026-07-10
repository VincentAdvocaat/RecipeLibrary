using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class ClearShoppingListCommandHandler(
    IShoppingListRepository repository,
    IShoppingListUserContext userContext)
    : ICommandHandler<ClearShoppingListCommand, ClearShoppingListResult>
{
    public async Task<ClearShoppingListResult> HandleAsync(
        ClearShoppingListCommand command,
        CancellationToken ct = default)
    {
        await ShoppingListAccessGuard.EnsureListAccessAsync(
            repository,
            command.ShoppingListId,
            userContext.OwnerUserId,
            ct);

        var list = await repository.GetListByIdAsync(command.ShoppingListId, ct);
        if (list is null)
        {
            return new ClearShoppingListResult(false);
        }

        await repository.ClearListItemsAsync(command.ShoppingListId, ct);
        return new ClearShoppingListResult(true);
    }
}
