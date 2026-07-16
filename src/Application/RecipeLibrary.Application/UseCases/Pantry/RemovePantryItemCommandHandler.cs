using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Pantry;

namespace RecipeLibrary.Application.UseCases.Pantry;

public sealed class RemovePantryItemCommandHandler(IPantryRepository repository)
    : ICommandHandler<RemovePantryItemCommand, RemovePantryItemResult>
{
    public async Task<RemovePantryItemResult> HandleAsync(
        RemovePantryItemCommand command,
        CancellationToken ct = default)
    {
        PantryOwnerKey.Validate(command.OwnerKey);
        var removed = await repository.RemoveAsync(command.ItemId, command.OwnerKey, ct);
        return new RemovePantryItemResult(removed);
    }
}
