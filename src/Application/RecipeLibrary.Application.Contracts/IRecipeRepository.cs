using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Contracts;

public interface IRecipeRepository
{
    Task AddAsync(Recipe recipe, CancellationToken ct = default);
}

