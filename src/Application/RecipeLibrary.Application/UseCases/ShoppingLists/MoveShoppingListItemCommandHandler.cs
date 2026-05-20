using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class MoveShoppingListItemCommandHandler(
    IShoppingListRepository repository,
    ShoppingListIngredientMerger merger)
    : ICommandHandler<MoveShoppingListItemCommand, MoveShoppingListItemResult>
{
    public async Task<MoveShoppingListItemResult> HandleAsync(
        MoveShoppingListItemCommand command,
        CancellationToken ct = default)
    {
        var item = await repository.GetItemByIdAsync(command.ItemId, ct)
            ?? throw new InvalidOperationException("Shopping list item not found.");

        if (item.ShoppingListId == command.TargetShoppingListId)
        {
            return new MoveShoppingListItemResult(true);
        }

        var sourceList = await repository.GetListByIdAsync(item.ShoppingListId, ct)
            ?? throw new InvalidOperationException("Source shopping list not found.");

        var targetList = await repository.GetListByIdAsync(command.TargetShoppingListId, ct)
            ?? throw new InvalidOperationException("Target shopping list not found.");

        if (sourceList.GroupId != targetList.GroupId)
        {
            throw new InvalidOperationException("Lists must belong to the same group.");
        }

        var sourceItems = sourceList.Items.Where(i => i.Id != item.Id).ToList();
        var targetItems = merger.MergeItemIntoList(
            targetList.Items.ToList(),
            item,
            targetList.Id);

        await repository.ReplaceListItemsAsync(sourceList.Id, sourceItems, ct);
        await repository.ReplaceListItemsAsync(targetList.Id, targetItems, ct);

        return new MoveShoppingListItemResult(true);
    }
}
