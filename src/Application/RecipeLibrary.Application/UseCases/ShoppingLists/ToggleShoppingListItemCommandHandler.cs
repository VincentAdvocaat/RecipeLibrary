using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class ToggleShoppingListItemCommandHandler(
    IShoppingListRepository repository,
    IPantryRepository pantryRepository,
    IShoppingListUserContext userContext,
    PantryIngredientMerger pantryMerger)
    : ICommandHandler<ToggleShoppingListItemCommand, ToggleShoppingListItemResult>
{
    public async Task<ToggleShoppingListItemResult> HandleAsync(
        ToggleShoppingListItemCommand command,
        CancellationToken ct = default)
    {
        await ShoppingListAccessGuard.EnsureItemAccessAsync(
            repository,
            command.ItemId,
            userContext.OwnerUserId,
            ct);

        var item = await repository.GetItemByIdAsync(command.ItemId, ct)
            ?? throw new InvalidOperationException("Shopping list item not found.");

        if (command.IsChecked && !item.IsChecked)
        {
            var list = await repository.GetListByIdAsync(item.ShoppingListId, ct)
                ?? throw new InvalidOperationException("Shopping list not found.");

            var ownerKey = PantryOwnerKey.Resolve(userContext.OwnerUserId, list.GroupId);
            var existingItems = await pantryRepository.GetByOwnerKeyAsync(ownerKey, ct);

            var merged = pantryMerger.MergeLineIntoPantry(
                existingItems,
                item.CanonicalIngredientId,
                item.DisplayName,
                item.Quantity.Value,
                item.Unit,
                ownerKey);

            await pantryRepository.UpsertAsync(merged, ct);
        }

        var updated = await repository.ToggleItemCheckedAsync(command.ItemId, command.IsChecked, ct);
        return new ToggleShoppingListItemResult(updated && command.IsChecked);
    }
}
