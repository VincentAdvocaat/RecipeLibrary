using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class ToggleShoppingListItemCommandHandler(IShoppingListRepository repository)
    : ICommandHandler<ToggleShoppingListItemCommand, ToggleShoppingListItemResult>
{
    public async Task<ToggleShoppingListItemResult> HandleAsync(
        ToggleShoppingListItemCommand command,
        CancellationToken ct = default)
    {
        var updated = await repository.ToggleItemCheckedAsync(command.ItemId, command.IsChecked, ct);
        return new ToggleShoppingListItemResult(updated && command.IsChecked);
    }
}
