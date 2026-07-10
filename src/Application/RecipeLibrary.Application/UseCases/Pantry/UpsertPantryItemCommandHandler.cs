using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

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

        var unit = UnitRules.ParseOrThrow(command.Unit);
        IngredientQuantityFormatter.ValidateQuantity(command.Quantity, unit);

        var existingItems = await repository.GetByOwnerKeyAsync(command.OwnerKey, ct);

        var merged = merger.MergeLineIntoPantry(
            existingItems,
            command.CanonicalIngredientId,
            displayName,
            command.Quantity,
            unit,
            command.OwnerKey);

        await repository.UpsertAsync(merged, ct);

        return new UpsertPantryItemResult(true, merged.Id);
    }
}
