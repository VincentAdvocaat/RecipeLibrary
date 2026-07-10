using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class GetPantryItemsQueryHandler(IPantryRepository repository)
    : IQueryHandler<GetPantryItemsQuery, GetPantryItemsResult>
{
    public async Task<GetPantryItemsResult> HandleAsync(GetPantryItemsQuery query, CancellationToken ct = default)
    {
        PantryOwnerKey.Validate(query.OwnerKey);
        var items = await repository.GetByOwnerKeyAsync(query.OwnerKey, ct);
        return PantryMapping.MapItems(items);
    }
}
