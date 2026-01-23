using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class EfRecipeRepository(RecipeDbContext dbContext) : IRecipeRepository
{
    public async Task AddAsync(Recipe recipe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        await dbContext.Recipes.AddAsync(recipe, ct);
        await dbContext.SaveChangesAsync(ct);
    }
}

