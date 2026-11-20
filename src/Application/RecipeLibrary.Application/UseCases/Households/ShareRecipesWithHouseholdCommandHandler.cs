using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Households;

public sealed class ShareRecipesWithHouseholdCommandHandler(
    IHouseholdRecipeRepository repository,
    ICurrentUser currentUser)
    : ICommandHandler<ShareRecipesWithHouseholdCommand, ShareRecipesWithHouseholdResult>
{
    public Task<ShareRecipesWithHouseholdResult> HandleAsync(
        ShareRecipesWithHouseholdCommand command, CancellationToken ct = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("Authentication is required.");
        return repository.ShareAsync(userId, command.HouseholdId, command.RecipeIds.Distinct().ToArray(), ct);
    }
}
