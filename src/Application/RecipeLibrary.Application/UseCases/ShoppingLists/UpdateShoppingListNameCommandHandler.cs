using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class UpdateShoppingListNameCommandHandler(
    IShoppingListRepository repository,
    ICurrentUser userContext)
    : ICommandHandler<UpdateShoppingListNameCommand, UpdateShoppingListNameResult>
{
    public async Task<UpdateShoppingListNameResult> HandleAsync(
        UpdateShoppingListNameCommand command,
        CancellationToken ct = default)
    {
        var name = (command.Name ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("List name is required.");
        }

        if (name.Length > 100)
        {
            throw new ArgumentException("List name must be at most 100 characters.");
        }

        await ShoppingListAccessGuard.EnsureListAccessAsync(
            repository,
            command.ShoppingListId,
            userContext.UserId,
            ct);

        var updated = await repository.UpdateListNameAsync(command.ShoppingListId, name, ct);
        return new UpdateShoppingListNameResult(updated);
    }
}
