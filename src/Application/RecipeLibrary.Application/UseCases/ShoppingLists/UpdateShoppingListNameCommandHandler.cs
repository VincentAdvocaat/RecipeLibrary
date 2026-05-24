using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class UpdateShoppingListNameCommandHandler(IShoppingListRepository repository)
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

        var updated = await repository.UpdateListNameAsync(command.ShoppingListId, name, ct);
        return new UpdateShoppingListNameResult(updated);
    }
}
