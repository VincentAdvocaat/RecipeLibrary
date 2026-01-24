using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Application.Abstractions;

public interface IRecipeRepository
{
    Task AddAsync(Recipe recipe, CancellationToken ct = default);
}

