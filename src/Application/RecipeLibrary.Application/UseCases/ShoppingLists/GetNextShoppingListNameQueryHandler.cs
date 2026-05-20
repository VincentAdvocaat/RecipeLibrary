using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class GetNextShoppingListNameQueryHandler(IShoppingListRepository repository)
    : IQueryHandler<GetNextShoppingListNameQuery, GetNextShoppingListNameResult>
{
    public async Task<GetNextShoppingListNameResult> HandleAsync(
        GetNextShoppingListNameQuery query,
        CancellationToken ct = default)
    {
        var format = (query.NameFormat ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(format))
        {
            throw new ArgumentException("Name format is required.");
        }

        var existingNames = await repository.GetListNamesAsync(query.ScopeGroupId, ct);
        var name = ShoppingListDefaultNameBuilder.GetNextNumberedName(format, existingNames);
        return new GetNextShoppingListNameResult(name);
    }
}
