using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class DeleteRecipeCommandHandler(
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser)
    : ICommandHandler<DeleteRecipeCommand, DeleteRecipeResult>
{
    public async Task<DeleteRecipeResult> HandleAsync(DeleteRecipeCommand command, CancellationToken ct = default)
    {
        var ownerUserId = currentUser.UserId
            ?? throw new UnauthorizedAccessException("Authentication is required.");

        var existing = await recipeRepository.GetByIdAsync(ownerUserId, command.RecipeId, ct);
        if (existing is null)
        {
            return new DeleteRecipeResult(false);
        }

        await recipeRepository.DeleteAsync(ownerUserId, command.RecipeId, ct);
        return new DeleteRecipeResult(true);
    }
}
