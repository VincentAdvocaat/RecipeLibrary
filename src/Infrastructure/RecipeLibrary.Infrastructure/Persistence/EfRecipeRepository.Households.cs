using System.Data;
using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Domain.Entities;

namespace RecipeLibrary.Infrastructure.Persistence;

public sealed partial class EfRecipeRepository
{
    public async Task<IReadOnlySet<Guid>> GetSharedSourceIdsAsync(string userId, Guid householdId, CancellationToken ct)
    {
        var ids = await AccessibleRecipes(dbContext, userId)
            .Where(recipe => recipe.HouseholdId == householdId && recipe.SharedFromRecipeId != null)
            .Select(recipe => recipe.SharedFromRecipeId!.Value)
            .ToListAsync(ct);
        return ids.ToHashSet();
    }

    public Task<ShareRecipesWithHouseholdResult> ShareAsync(
        string userId, Guid householdId, IReadOnlyList<Guid> recipeIds, CancellationToken ct) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            try
            {
                if (!await dbContext.HouseholdMembers.AnyAsync(member =>
                        member.UserId == userId && member.HouseholdId == householdId, ct))
                {
                    throw new UnauthorizedAccessException("Household access is required.");
                }

                var sources = await OwnedRecipes(userId).AsNoTracking()
                    .Where(recipe => recipeIds.Contains(recipe.Id))
                    .Include(recipe => recipe.Ingredients)
                    .Include(recipe => recipe.InstructionSteps)
                    .AsSplitQuery()
                    .ToListAsync(ct);
                var shared = await GetSharedSourceIdsAsync(userId, householdId, ct);
                var copies = sources.Where(source => !shared.Contains(source.Id))
                    .Select(source =>
                    {
                        var copy = source.CopyFor(source.OwnerUserId);
                        copy.HouseholdId = householdId;
                        copy.SharedFromRecipeId = source.Id;
                        return copy;
                    }).ToList();
                dbContext.Recipes.AddRange(copies);
                await dbContext.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
                dbContext.ChangeTracker.Clear();
                return new ShareRecipesWithHouseholdResult(copies.Count, sources.Count - copies.Count,
                    recipeIds.Count - sources.Count);
            }
            catch
            {
                dbContext.ChangeTracker.Clear();
                throw;
            }
        });
}
