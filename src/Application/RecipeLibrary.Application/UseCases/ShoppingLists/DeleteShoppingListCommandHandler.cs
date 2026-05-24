using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class DeleteShoppingListCommandHandler(IShoppingListRepository repository)
    : ICommandHandler<DeleteShoppingListCommand, DeleteShoppingListResult>
{
    public async Task<DeleteShoppingListResult> HandleAsync(
        DeleteShoppingListCommand command,
        CancellationToken ct = default)
    {
        var list = await repository.GetListByIdAsync(command.ShoppingListId, ct);
        if (list is null)
        {
            return new DeleteShoppingListResult(false, null);
        }

        var groupId = list.GroupId;
        await repository.DeleteListAsync(command.ShoppingListId, ct);

        var group = await repository.GetGroupWithListsAsync(groupId, ct);
        return new DeleteShoppingListResult(true, group?.Id);
    }
}
