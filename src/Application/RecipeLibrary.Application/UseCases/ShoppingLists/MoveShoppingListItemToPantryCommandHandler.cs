using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class MoveShoppingListItemToPantryCommandHandler(
    IShoppingListRepository shoppingListRepository,
    IPantryRepository pantryRepository,
    IShoppingListUserContext userContext,
    PantryIngredientMerger pantryMerger)
    : ICommandHandler<MoveShoppingListItemToPantryCommand, MoveShoppingListItemToPantryResult>
{
    public async Task<MoveShoppingListItemToPantryResult> HandleAsync(
        MoveShoppingListItemToPantryCommand command,
        CancellationToken ct = default)
    {
        PantryOwnerKey.Validate(command.OwnerKey);

        await ShoppingListAccessGuard.EnsureItemAccessAsync(
            shoppingListRepository,
            command.ItemId,
            userContext.OwnerUserId,
            ct);

        var item = await shoppingListRepository.GetItemByIdAsync(command.ItemId, ct)
            ?? throw new InvalidOperationException("Shopping list item not found.");

        var existingItems = await pantryRepository.GetByOwnerKeyAsync(command.OwnerKey, ct);
        var pantryItem = pantryMerger.EnsurePresent(
            existingItems,
            item.CanonicalIngredientId,
            item.DisplayName,
            command.OwnerKey);

        await pantryRepository.UpsertAsync(pantryItem, ct);

        var removed = await shoppingListRepository.RemoveItemAsync(command.ItemId, ct);
        return new MoveShoppingListItemToPantryResult(removed, pantryItem.Id);
    }
}
