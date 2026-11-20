using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class UpsertPantryItemCommandHandler(
    IPantryRepository repository,
    IShoppingListRepository shoppingListRepository,
    ICurrentUser userContext,
    PantryIngredientMerger merger,
    IUnitOfWork unitOfWork,
    IHouseholdRepository? households = null)
    : ICommandHandler<UpsertPantryItemCommand, UpsertPantryItemResult>
{
    public async Task<UpsertPantryItemResult> HandleAsync(
        UpsertPantryItemCommand command,
        CancellationToken ct = default)
    {
        var ownerKey = await PantryOwnerKey.ResolveForLibraryAsync(shoppingListRepository, households,
            userContext.UserId, command.ShoppingListGroupId, command.HouseholdId, ct);

        var displayName = (command.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            throw new ArgumentException("Display name is required.");
        }

        var existingItems = await repository.GetByOwnerKeyAsync(ownerKey, ct);

        var item = merger.EnsurePresent(
            existingItems,
            command.CanonicalIngredientId,
            displayName,
            ownerKey);

        await repository.UpsertAsync(item, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new UpsertPantryItemResult(true, item.Id);
    }
}
