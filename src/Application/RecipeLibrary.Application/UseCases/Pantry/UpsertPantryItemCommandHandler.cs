using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class UpsertPantryItemCommandHandler(
    IPantryRepository repository,
    IShoppingListRepository shoppingListRepository,
    ICurrentUser userContext,
    PantryIngredientMerger merger)
    : ICommandHandler<UpsertPantryItemCommand, UpsertPantryItemResult>
{
    public async Task<UpsertPantryItemResult> HandleAsync(
        UpsertPantryItemCommand command,
        CancellationToken ct = default)
    {
        await ShoppingListAccessGuard.EnsureGroupAccessAsync(
            shoppingListRepository,
            command.ShoppingListGroupId,
            userContext.UserId,
            ct);

        var displayName = (command.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            throw new ArgumentException("Display name is required.");
        }

        var ownerKey = PantryOwnerKey.Resolve(userContext.UserId, command.ShoppingListGroupId);
        var existingItems = await repository.GetByOwnerKeyAsync(ownerKey, ct);

        var item = merger.EnsurePresent(
            existingItems,
            command.CanonicalIngredientId,
            displayName,
            ownerKey);

        await repository.UpsertAsync(item, ct);

        return new UpsertPantryItemResult(true, item.Id);
    }
}
