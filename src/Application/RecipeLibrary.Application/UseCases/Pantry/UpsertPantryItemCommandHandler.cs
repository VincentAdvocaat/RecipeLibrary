using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class UpsertPantryItemCommandHandler(
    IPantryRepository repository,
    PantryIngredientMerger merger)
    : ICommandHandler<UpsertPantryItemCommand, UpsertPantryItemResult>
{
    public async Task<UpsertPantryItemResult> HandleAsync(
        UpsertPantryItemCommand command,
        CancellationToken ct = default)
    {
        PantryOwnerKey.Validate(command.OwnerKey);

        var displayName = (command.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            throw new ArgumentException("Display name is required.");
        }

        var existingItems = await repository.GetByOwnerKeyAsync(command.OwnerKey, ct);

        var item = merger.EnsurePresent(
            existingItems,
            command.CanonicalIngredientId,
            displayName,
            command.OwnerKey);

        await repository.UpsertAsync(item, ct);

        return new UpsertPantryItemResult(true, item.Id);
    }
}
