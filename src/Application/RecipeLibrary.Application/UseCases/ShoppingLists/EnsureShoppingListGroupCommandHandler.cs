using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.ShoppingLists;

namespace RecipeLibrary.Application.UseCases.ShoppingLists;

public sealed class EnsureShoppingListGroupCommandHandler(
    IShoppingListRepository repository,
    IUnitOfWork? unitOfWork = null)
    : ICommandHandler<EnsureShoppingListGroupCommand, EnsureShoppingListGroupResult>
{
    public async Task<EnsureShoppingListGroupResult> HandleAsync(
        EnsureShoppingListGroupCommand command,
        CancellationToken ct = default)
    {
        var nameFormat = (command.DefaultListNameFormat ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(nameFormat))
        {
            throw new ArgumentException("Default list name format is required.");
        }

        if (string.IsNullOrWhiteSpace(command.OwnerUserId))
        {
            throw new UnauthorizedAccessException("Authentication is required to get or create a shopping list group.");
        }

        var ownedGroup = await repository.GetGroupByOwnerUserIdAsync(command.OwnerUserId, ct);
        if (ownedGroup is not null)
        {
            return ShoppingListMapping.MapGroup(ownedGroup);
        }

        var existingNames = await repository.GetListNamesAsync(groupId: null, ct);
        var defaultName = ShoppingListDefaultNameBuilder.GetNextNumberedName(nameFormat, existingNames);
        var created = await repository.CreateGroupWithPrimaryListAsync(defaultName, command.OwnerUserId, ct);
        await (unitOfWork?.SaveChangesAsync(ct) ?? Task.CompletedTask);
        var loaded = await repository.GetGroupWithListsAsync(created.Id, ct)
            ?? throw new InvalidOperationException("Failed to load created shopping list group.");

        return ShoppingListMapping.MapGroup(loaded);
    }
}
