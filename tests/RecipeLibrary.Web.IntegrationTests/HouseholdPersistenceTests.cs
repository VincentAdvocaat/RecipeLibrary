using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Infrastructure.Identity;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class HouseholdPersistenceTests(SqlContainerFixture fixture)
{
    [Fact]
    public async Task HouseholdCopiesDeduplicateAndMembersRateIndependently()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RecipeDbContext>();
        var manager = new ApplicationUser { Id = Guid.NewGuid().ToString() };
        var member = new ApplicationUser { Id = Guid.NewGuid().ToString() };
        var outsider = new ApplicationUser { Id = Guid.NewGuid().ToString() };
        db.Users.AddRange(manager, member, outsider);
        var original = new Recipe
        {
            Id = Guid.NewGuid(),
            OwnerUserId = manager.Id,
            Title = new RecipeTitle("Household source"),
            ImageUrl = $"/api/recipe-images/{Guid.NewGuid():N}.png",
        };
        db.Recipes.Add(original);
        await db.SaveChangesAsync();

        var households = services.GetRequiredService<IHouseholdRepository>();
        Assert.True(await households.CreateAsync(manager.Id, "Household", default));
        var household = (await households.GetAsync(manager.Id, default))!;
        var now = DateTimeOffset.UtcNow;
        const string tokenHash = "test-invitation";
        Assert.True(await households.CreateInvitationAsync(manager.Id, tokenHash, now.AddHours(1), default));
        Assert.Equal(JoinHouseholdResult.Joined, await households.JoinAsync(member.Id, tokenHash, now, default));
        Assert.Equal(JoinHouseholdResult.InvalidInvitation, await households.JoinAsync(outsider.Id, tokenHash, now, default));

        var recipes = services.GetRequiredService<IRecipeRepository>();
        var householdRecipes = services.GetRequiredService<IHouseholdRecipeRepository>();
        var ratings = services.GetRequiredService<IRecipeRatingRepository>();
        var managerRating = new SetRecipeRatingCommandHandler(ratings, new FixedCurrentUser(manager.Id));
        var memberRating = new SetRecipeRatingCommandHandler(ratings, new FixedCurrentUser(member.Id));
        Assert.True((await managerRating.HandleAsync(new(original.Id, 5))).Saved);

        var recipeIds = new[] { original.Id, Guid.NewGuid() };
        Assert.Equal(new ShareRecipesWithHouseholdResult(1, 0, 1),
            await householdRecipes.ShareAsync(manager.Id, household.Id, recipeIds, default));
        Assert.Equal(new ShareRecipesWithHouseholdResult(0, 1, 1),
            await householdRecipes.ShareAsync(manager.Id, household.Id, recipeIds, default));
        var copy = Assert.Single(await householdRecipes.GetHouseholdListAsync(member.Id, household.Id, null, null, default));
        Assert.NotEqual(original.Id, copy.Id);
        Assert.Equal(original.ImageUrl, copy.ImageUrl);
        Assert.Null(await recipes.GetByIdAsync(member.Id, original.Id));
        Assert.Null(await recipes.GetByIdAsync(outsider.Id, copy.Id));
        Assert.Empty((await recipes.GetByIdAsync(manager.Id, copy.Id))!.Ratings);

        Assert.True((await managerRating.HandleAsync(new(copy.Id, 4))).Saved);
        Assert.True((await memberRating.HandleAsync(new(copy.Id, 2))).Saved);
        Assert.Equal(4, Assert.Single((await recipes.GetByIdAsync(manager.Id, copy.Id))!.Ratings).Stars);
        Assert.Equal(2, Assert.Single((await recipes.GetByIdAsync(member.Id, copy.Id))!.Ratings).Stars);
        Assert.True((await managerRating.HandleAsync(new(copy.Id, 0))).Saved);
        Assert.Equal(2, Assert.Single(await db.RecipeRatings.Where(r => r.RecipeId == copy.Id).ToListAsync()).Stars);
        Assert.Equal(5, Assert.Single((await recipes.GetByIdAsync(manager.Id, original.Id))!.Ratings).Stars);

        var imageKey = Path.GetFileName(original.ImageUrl)!;
        await recipes.DeleteAsync(manager.Id, original.Id);
        Assert.True(await recipes.IsRecipeImageAccessibleAsync(member.Id, imageKey));
        Assert.False(await recipes.IsRecipeImageAccessibleAsync(outsider.Id, imageKey));
        Assert.True(await households.LeaveAsync(member.Id, default));
        Assert.Null(await recipes.GetByIdAsync(member.Id, copy.Id));
        Assert.False((await memberRating.HandleAsync(new(copy.Id, 0))).Saved);
        Assert.False(await recipes.IsRecipeImageAccessibleAsync(member.Id, imageKey));
    }
}
