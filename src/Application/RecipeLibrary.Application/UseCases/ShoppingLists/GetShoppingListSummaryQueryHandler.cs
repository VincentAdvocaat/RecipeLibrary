using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class GetShoppingListSummaryQueryHandler(
    IShoppingListRepository repository,
    IShoppingListUserContext userContext)
    : IQueryHandler<GetShoppingListSummaryQuery, ShoppingListSummaryResult>
{
    public async Task<ShoppingListSummaryResult> HandleAsync(
        GetShoppingListSummaryQuery query,
        CancellationToken ct = default)
    {
        await ShoppingListAccessGuard.EnsureGroupAccessAsync(
            repository,
            query.GroupId,
            userContext.OwnerUserId,
            ct);

        var count = await repository.GetUncheckedItemCountForGroupAsync(query.GroupId, ct);
        return new ShoppingListSummaryResult(count);
    }
}
