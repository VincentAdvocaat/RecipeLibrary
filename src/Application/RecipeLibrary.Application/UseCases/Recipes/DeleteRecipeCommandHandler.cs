using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class DeleteRecipeCommandHandler(IRecipeRepository recipeRepository)
    : ICommandHandler<DeleteRecipeCommand, DeleteRecipeResult>
{
    public async Task<DeleteRecipeResult> HandleAsync(DeleteRecipeCommand command, CancellationToken ct = default)
    {
        var existing = await recipeRepository.GetByIdAsync(command.RecipeId, ct);
        if (existing is null)
        {
            return new DeleteRecipeResult(false);
        }

        await recipeRepository.DeleteAsync(command.RecipeId, ct);
        return new DeleteRecipeResult(true);
    }
}
