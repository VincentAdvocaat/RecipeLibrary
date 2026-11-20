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
        Guid itemId = default;
        async Task UpsertAsync(CancellationToken operationCt)
        {
            var ownerKey = await PantryOwnerKey.ResolveForLibraryAsync(shoppingListRepository, households,
                userContext.UserId, command.ShoppingListGroupId, command.HouseholdId, operationCt);
            var displayName = (command.DisplayName ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(displayName))
                throw new ArgumentException("Display name is required.");

            var existingItems = await repository.GetByOwnerKeyAsync(ownerKey, operationCt);
            var item = merger.EnsurePresent(existingItems, command.CanonicalIngredientId, displayName, ownerKey);
            await repository.UpsertAsync(item, operationCt);
            itemId = item.Id;
        }

        if (command.HouseholdId.HasValue)
            await unitOfWork.ExecuteInSerializableTransactionAsync(UpsertAsync, ct);
        else
        {
            await UpsertAsync(ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return new UpsertPantryItemResult(true, itemId);
    }
}
