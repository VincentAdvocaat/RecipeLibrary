using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;

namespace RecipeLibrary.Application.UseCases.Recipes;

public sealed class DeleteRecipeCommandHandler(IRecipeRepository recipeRepository)
    : ICommandHandler<DeleteRecipeCommand, DeleteRecipeResult>
{
    public async Task<DeleteRecipeResult> HandleAsync(DeleteRecipeCommand command, CancellationToken ct = default)
    {
        await recipeRepository.DeleteAsync(command.RecipeId, ct);

        var stillExists = await recipeRepository.GetByIdAsync(command.RecipeId, ct);
        return new DeleteRecipeResult(stillExists is null);
    }
}
