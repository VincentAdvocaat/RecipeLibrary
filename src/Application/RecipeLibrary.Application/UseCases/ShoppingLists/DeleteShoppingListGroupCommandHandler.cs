using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class DeleteShoppingListGroupCommandHandler(IShoppingListRepository repository)
    : ICommandHandler<DeleteShoppingListGroupCommand, DeleteShoppingListGroupResult>
{
    public async Task<DeleteShoppingListGroupResult> HandleAsync(
        DeleteShoppingListGroupCommand command,
        CancellationToken ct = default)
    {
        await repository.DeleteGroupAsync(command.GroupId, ct);
        return new DeleteShoppingListGroupResult(true);
    }
}
