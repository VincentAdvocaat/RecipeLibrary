using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed class EfRecipeRepository(RecipeDbContext dbContext) : IRecipeRepository
{
    public async Task AddAsync(Recipe recipe, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recipe);

        await dbContext.Recipes.AddAsync(recipe, ct);
        await dbContext.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<Recipe>> GetListAsync(string? search, RecipeCategory? category, CancellationToken ct = default)
    {
        var query = dbContext.Recipes
            .Include(r => r.Ingredients)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(r =>
                EF.Property<string>(r, "Title").Contains(term) ||
                (r.Description != null && r.Description.Contains(term)));
        }

        if (category is { } cat)
        {
            query = query.Where(r => r.Category == cat);
        }

        return await query.OrderBy(r => r.Title).ToListAsync(ct);
    }
}

