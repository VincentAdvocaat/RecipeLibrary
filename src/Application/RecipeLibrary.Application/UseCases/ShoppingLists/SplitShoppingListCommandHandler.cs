using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class SplitShoppingListCommandHandler(
    IShoppingListRepository repository,
    ShoppingListIngredientMerger merger)
    : ICommandHandler<SplitShoppingListCommand, SplitShoppingListResult>
{
    public async Task<SplitShoppingListResult> HandleAsync(
        SplitShoppingListCommand command,
        CancellationToken ct = default)
    {
        var name = (command.NewListName ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("New list name is required.");
        }

        if (command.ItemIds.Count == 0)
        {
            throw new ArgumentException("At least one item must be selected.");
        }

        if (await repository.GroupHasSecondListAsync(command.GroupId, ct))
        {
            throw new InvalidOperationException("This group already has a second shopping list.");
        }

        var primary = await repository.GetPrimaryListInGroupAsync(command.GroupId, ct)
            ?? throw new InvalidOperationException("Primary shopping list not found.");

        var selectedIds = command.ItemIds.ToHashSet();
        var selected = primary.Items.Where(i => selectedIds.Contains(i.Id)).ToList();

        if (selected.Count != selectedIds.Count)
        {
            throw new ArgumentException("One or more items do not belong to the primary shopping list.");
        }

        var remaining = primary.Items.Where(i => !selectedIds.Contains(i.Id)).ToList();

        var secondary = await repository.AddListToGroupAsync(command.GroupId, name, storeOrder: 2, ct);

        var secondaryItems = new List<ShoppingListItem>();
        foreach (var item in selected)
        {
            secondaryItems = merger.MergeItemIntoList(secondaryItems, item, secondary.Id).ToList();
        }

        await repository.ReplaceListItemsAsync(secondary.Id, secondaryItems, ct);
        await repository.ReplaceListItemsAsync(primary.Id, remaining, ct);

        return new SplitShoppingListResult(secondary.Id, selected.Count);
    }
}
