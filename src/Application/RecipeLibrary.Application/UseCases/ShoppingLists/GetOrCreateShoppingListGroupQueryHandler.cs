using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class GetOrCreateShoppingListGroupQueryHandler(IShoppingListRepository repository)
    : IQueryHandler<GetOrCreateShoppingListGroupQuery, GetOrCreateShoppingListGroupResult>
{
    public async Task<GetOrCreateShoppingListGroupResult> HandleAsync(
        GetOrCreateShoppingListGroupQuery query,
        CancellationToken ct = default)
    {
        var nameFormat = (query.DefaultListNameFormat ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(nameFormat))
        {
            throw new ArgumentException("Default list name format is required.");
        }

        if (string.IsNullOrWhiteSpace(query.OwnerUserId))
        {
            throw new UnauthorizedAccessException("Authentication is required to get or create a shopping list group.");
        }

        var ownedGroup = await repository.GetGroupByOwnerUserIdAsync(query.OwnerUserId, ct);
        if (ownedGroup is not null)
        {
            return ShoppingListMapping.MapGroup(ownedGroup);
        }

        var existingNames = await repository.GetListNamesAsync(groupId: null, ct);
        var defaultName = ShoppingListDefaultNameBuilder.GetNextNumberedName(nameFormat, existingNames);
        var created = await repository.CreateGroupWithPrimaryListAsync(defaultName, query.OwnerUserId, ct);
        var loaded = await repository.GetGroupWithListsAsync(created.Id, ct)
            ?? throw new InvalidOperationException("Failed to load created shopping list group.");

        return ShoppingListMapping.MapGroup(loaded);
    }
}
