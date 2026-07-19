using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class ToggleShoppingListItemCommandHandler(
    IShoppingListRepository repository,
    ICurrentUser userContext)
    : ICommandHandler<ToggleShoppingListItemCommand, ToggleShoppingListItemResult>
{
    public async Task<ToggleShoppingListItemResult> HandleAsync(
        ToggleShoppingListItemCommand command,
        CancellationToken ct = default)
    {
        await ShoppingListAccessGuard.EnsureItemAccessAsync(
            repository,
            command.ItemId,
            userContext.UserId,
            ct);

        var updated = await repository.ToggleItemCheckedAsync(command.ItemId, command.IsChecked, ct);
        return new ToggleShoppingListItemResult(updated && command.IsChecked);
    }
}
