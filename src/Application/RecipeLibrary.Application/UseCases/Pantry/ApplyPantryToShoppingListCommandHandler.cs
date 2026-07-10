using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class ApplyPantryToShoppingListCommandHandler(
    IShoppingListRepository shoppingListRepository,
    IPantryRepository pantryRepository,
    IShoppingListUserContext userContext,
    PantrySubtractor subtractor)
    : ICommandHandler<ApplyPantryToShoppingListCommand, ApplyPantryToShoppingListResult>
{
    public async Task<ApplyPantryToShoppingListResult> HandleAsync(
        ApplyPantryToShoppingListCommand command,
        CancellationToken ct = default)
    {
        PantryOwnerKey.Validate(command.OwnerKey);

        await ShoppingListAccessGuard.EnsureListAccessAsync(
            shoppingListRepository,
            command.ShoppingListId,
            userContext.OwnerUserId,
            ct);

        var list = await shoppingListRepository.GetListByIdAsync(command.ShoppingListId, ct)
            ?? throw new InvalidOperationException("Shopping list not found.");

        var pantryItems = await pantryRepository.GetByOwnerKeyAsync(command.OwnerKey, ct);
        if (pantryItems.Count == 0)
        {
            return new ApplyPantryToShoppingListResult(0, 0);
        }

        var originalCount = list.Items.Count;
        var adjusted = subtractor.SubtractFromListItems(list.Items.ToList(), pantryItems);
        var removed = originalCount - adjusted.Count;

        await shoppingListRepository.ReplaceListItemsAsync(list.Id, adjusted, ct);

        return new ApplyPantryToShoppingListResult(adjusted.Count, removed);
    }
}
