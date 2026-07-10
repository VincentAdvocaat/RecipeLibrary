using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class DeleteShoppingListCommandHandler(
    IShoppingListRepository repository,
    IShoppingListUserContext userContext)
    : ICommandHandler<DeleteShoppingListCommand, DeleteShoppingListResult>
{
    public async Task<DeleteShoppingListResult> HandleAsync(
        DeleteShoppingListCommand command,
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
            return new DeleteShoppingListResult(false, null);
        }

        var groupId = list.GroupId;
        await repository.DeleteListAsync(command.ShoppingListId, ct);

        var group = await repository.GetGroupWithListsAsync(groupId, ct);
        return new DeleteShoppingListResult(true, group?.Id);
    }
}
