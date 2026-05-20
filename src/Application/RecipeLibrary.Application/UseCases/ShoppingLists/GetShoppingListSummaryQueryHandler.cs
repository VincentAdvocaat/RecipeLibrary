using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class GetShoppingListSummaryQueryHandler(IShoppingListRepository repository)
    : IQueryHandler<GetShoppingListSummaryQuery, ShoppingListSummaryResult>
{
    public async Task<ShoppingListSummaryResult> HandleAsync(
        GetShoppingListSummaryQuery query,
        CancellationToken ct = default)
    {
        var count = await repository.GetUncheckedItemCountForGroupAsync(query.GroupId, ct);
        return new ShoppingListSummaryResult(count);
    }
}
