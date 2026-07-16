using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class ApplyPantryToShoppingListCommandHandler(
    IShoppingListRepository shoppingListRepository,
    IPantryRepository pantryRepository,
    IShoppingListUserContext userContext,
    PantryExclusionFilter exclusionFilter)
    : ICommandHandler<ApplyPantryToShoppingListCommand, ApplyPantryToShoppingListResult>
{
    public async Task<ApplyPantryToShoppingListResult> HandleAsync(
        ApplyPantryToShoppingListCommand command,
        CancellationToken ct = default)
    {
        await ShoppingListAccessGuard.EnsureListAccessAsync(
            shoppingListRepository,
            command.ShoppingListId,
            userContext.OwnerUserId,
            ct);

        var list = await shoppingListRepository.GetListByIdAsync(command.ShoppingListId, ct)
            ?? throw new InvalidOperationException("Shopping list not found.");

        var ownerKey = PantryOwnerKey.Resolve(userContext.OwnerUserId, list.GroupId);
        var pantryItems = await pantryRepository.GetByOwnerKeyAsync(ownerKey, ct);
        if (pantryItems.Count == 0)
        {
            return new ApplyPantryToShoppingListResult(0);
        }

        var originalCount = list.Items.Count;
        var remaining = exclusionFilter.ExcludeMatchingItems(list.Items.ToList(), pantryItems);
        var removed = originalCount - remaining.Count;

        await shoppingListRepository.ReplaceListItemsAsync(list.Id, remaining, ct);

        return new ApplyPantryToShoppingListResult(removed);
    }
}
