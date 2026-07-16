using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class UpdateShoppingListItemQuantityCommandHandler(IShoppingListRepository repository)
    : ICommandHandler<UpdateShoppingListItemQuantityCommand, UpdateShoppingListItemQuantityResult>
{
    public async Task<UpdateShoppingListItemQuantityResult> HandleAsync(
        UpdateShoppingListItemQuantityCommand command,
        CancellationToken ct = default)
    {
        var item = await repository.GetItemByIdAsync(command.ItemId, ct)
            ?? throw new InvalidOperationException("Shopping list item not found.");

        if (item.Unit is null)
        {
            throw new InvalidOperationException("Cannot set quantity on an unmeasured shopping list item.");
        }

        IngredientQuantityFormatter.ValidateQuantity(command.Quantity, item.Unit.Value);

        var normalized = IngredientQuantityFormatter.Normalize(command.Quantity, item.Unit.Value);
        var updated = await repository.UpdateItemQuantityAsync(command.ItemId, normalized, ct);
        return new UpdateShoppingListItemQuantityResult(updated, normalized);
    }
}
