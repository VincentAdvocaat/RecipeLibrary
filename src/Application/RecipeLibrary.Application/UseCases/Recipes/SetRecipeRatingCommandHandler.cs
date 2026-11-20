using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class SetRecipeRatingCommandHandler(
    IRecipeRatingRepository recipeRepository,
    ICurrentUser currentUser)
    : ICommandHandler<SetRecipeRatingCommand, SetRecipeRatingResult>
{
    public async Task<SetRecipeRatingResult> HandleAsync(SetRecipeRatingCommand command, CancellationToken ct = default)
    {
        var userId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        ArgumentOutOfRangeException.ThrowIfNegative(command.Stars);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(command.Stars, 5);

        var saved = await recipeRepository.TrySetRatingAsync(userId, command.RecipeId, command.Stars, ct);
        return new SetRecipeRatingResult(saved);
    }
}
