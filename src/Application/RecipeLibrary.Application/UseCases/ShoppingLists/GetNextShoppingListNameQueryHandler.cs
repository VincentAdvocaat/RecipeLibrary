using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class GetNextShoppingListNameQueryHandler(
    IShoppingListRepository repository,
    ICurrentUser userContext)
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

        Guid scopeGroupId;
        if (query.ScopeGroupId is Guid requested && requested != Guid.Empty)
        {
            await ShoppingListAccessGuard.EnsureGroupAccessAsync(
                repository,
                requested,
                userContext.UserId,
                ct);
            scopeGroupId = requested;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(userContext.UserId))
            {
                throw new UnauthorizedAccessException("Authentication is required to access shopping lists.");
            }

            var owned = await repository.GetGroupByOwnerUserIdAsync(userContext.UserId, ct);
            if (owned is null)
            {
                return new GetNextShoppingListNameResult(
                    ShoppingListDefaultNameBuilder.GetNextNumberedName(format, []));
            }

            scopeGroupId = owned.Id;
        }

        var existingNames = await repository.GetListNamesAsync(scopeGroupId, ct);
        var name = ShoppingListDefaultNameBuilder.GetNextNumberedName(format, existingNames);
        return new GetNextShoppingListNameResult(name);
    }
}
