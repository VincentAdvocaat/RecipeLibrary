using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class UpdatePantryItemQuantityCommandHandler(IPantryRepository repository)
    : ICommandHandler<UpdatePantryItemQuantityCommand, UpdatePantryItemQuantityResult>
{
    public async Task<UpdatePantryItemQuantityResult> HandleAsync(
        UpdatePantryItemQuantityCommand command,
        CancellationToken ct = default)
    {
        PantryOwnerKey.Validate(command.OwnerKey);

        var item = await repository.GetByIdForOwnerAsync(command.ItemId, command.OwnerKey, ct)
            ?? throw new InvalidOperationException("Pantry item not found.");

        IngredientQuantityFormatter.ValidateQuantity(command.Quantity, item.Unit);

        var normalized = IngredientQuantityFormatter.Normalize(command.Quantity, item.Unit);
        var updated = await repository.UpdateQuantityAsync(command.ItemId, command.OwnerKey, normalized, ct);
        return new UpdatePantryItemQuantityResult(updated, normalized);
    }
}
