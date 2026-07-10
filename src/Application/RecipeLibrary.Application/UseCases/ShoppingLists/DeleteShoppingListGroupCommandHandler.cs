using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class DeleteShoppingListGroupCommandHandler(
    IShoppingListRepository repository,
    IShoppingListUserContext userContext)
    : ICommandHandler<DeleteShoppingListGroupCommand, DeleteShoppingListGroupResult>
{
    public async Task<DeleteShoppingListGroupResult> HandleAsync(
        DeleteShoppingListGroupCommand command,
        CancellationToken ct = default)
    {
        await ShoppingListAccessGuard.EnsureGroupAccessAsync(
            repository,
            command.GroupId,
            userContext.OwnerUserId,
            ct);

        await repository.DeleteGroupAsync(command.GroupId, ct);
        return new DeleteShoppingListGroupResult(true);
    }
}
