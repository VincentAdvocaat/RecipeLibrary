using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class AddManualShoppingListItemCommandHandler(
    IShoppingListRepository shoppingListRepository,
    ShoppingListIngredientMerger merger)
    : ICommandHandler<AddManualShoppingListItemCommand, AddManualShoppingListItemResult>
{
    public async Task<AddManualShoppingListItemResult> HandleAsync(
        AddManualShoppingListItemCommand command,
        CancellationToken ct = default)
    {
        var displayName = (command.DisplayName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(displayName))
        {
            throw new ArgumentException("Display name is required.");
        }

        if (displayName.Length > 200)
        {
            throw new ArgumentException("Display name must be at most 200 characters.");
        }

        var preparation = string.IsNullOrWhiteSpace(command.Preparation)
            ? null
            : command.Preparation.Trim();

        if (preparation?.Length > 200)
        {
            throw new ArgumentException("Preparation must be at most 200 characters.");
        }

        Unit? unit = null;
        decimal? quantity = null;
        if (!IngredientMeasure.IsUnmeasured(command.Quantity, command.Unit))
        {
            if (command.Quantity is null || string.IsNullOrWhiteSpace(command.Unit))
            {
                throw new ArgumentException("Quantity and unit are both required unless the item is unmeasured.");
            }

            unit = UnitRules.ParseOrThrow(command.Unit);
            IngredientQuantityFormatter.ValidateQuantity(command.Quantity.Value, unit.Value);
            quantity = command.Quantity.Value;
        }

        var list = await shoppingListRepository.GetListByIdAsync(command.ShoppingListId, ct)
            ?? throw new InvalidOperationException("Shopping list not found.");

        var merged = merger.MergeManualLineIntoList(
            list.Items.ToList(),
            command.CanonicalIngredientId,
            displayName,
            preparation,
            quantity,
            unit,
            list.Id);

        var previousIds = list.Items.Select(i => i.Id).ToHashSet();
        var addedItem = merged.FirstOrDefault(i => !previousIds.Contains(i.Id));

        await shoppingListRepository.ReplaceListItemsAsync(list.Id, merged, ct);

        return new AddManualShoppingListItemResult(true, addedItem?.Id);
    }
}
