using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class MoveShoppingListItemToPantryCommandHandler(
    IShoppingListRepository shoppingListRepository,
    IPantryRepository pantryRepository,
    IShoppingListUserContext userContext,
    IUnitOfWork unitOfWork,
    PantryIngredientMerger pantryMerger)
    : ICommandHandler<MoveShoppingListItemToPantryCommand, MoveShoppingListItemToPantryResult>
{
    public async Task<MoveShoppingListItemToPantryResult> HandleAsync(
        MoveShoppingListItemToPantryCommand command,
        CancellationToken ct = default)
    {
        await ShoppingListAccessGuard.EnsureItemAccessAsync(
            shoppingListRepository,
            command.ItemId,
            userContext.OwnerUserId,
            ct);

        var item = await shoppingListRepository.GetItemByIdAsync(command.ItemId, ct)
            ?? throw new InvalidOperationException("Shopping list item not found.");

        var list = await shoppingListRepository.GetListByIdAsync(item.ShoppingListId, ct)
            ?? throw new InvalidOperationException("Shopping list not found.");

        var ownerKey = PantryOwnerKey.Resolve(userContext.OwnerUserId, list.GroupId);
        var existingItems = await pantryRepository.GetByOwnerKeyAsync(ownerKey, ct);
        var pantryItem = pantryMerger.EnsurePresent(
            existingItems,
            item.CanonicalIngredientId,
            item.DisplayName,
            ownerKey);

        var removed = false;
        await unitOfWork.ExecuteInTransactionAsync(async transactionCt =>
        {
            await pantryRepository.UpsertAsync(pantryItem, transactionCt);
            removed = await shoppingListRepository.RemoveItemAsync(command.ItemId, transactionCt);
        }, ct);

        return new MoveShoppingListItemToPantryResult(removed, pantryItem.Id);
    }
}
