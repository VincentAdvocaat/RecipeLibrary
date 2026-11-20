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
public sealed class RecipeRatingPersistenceTests(SqlContainerFixture fixture)
{
    [Fact]
    public async Task OwnerCanRateChangeAndClearWithoutChangingTheRecipe()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        var userId = Guid.NewGuid().ToString();
        var recipeId = Guid.NewGuid();
        var updatedAt = DateTimeOffset.UtcNow;
        db.Users.Add(new ApplicationUser { Id = userId });
        db.Recipes.Add(new Recipe
        {
            Id = recipeId,
            OwnerUserId = userId,
            Title = new RecipeTitle("Recipe to rate"),
            UpdatedAt = updatedAt,
        });
        await db.SaveChangesAsync();

        var ratings = scope.ServiceProvider.GetRequiredService<IRecipeRatingRepository>();
        var recipes = scope.ServiceProvider.GetRequiredService<IRecipeRepository>();
        var user = new FixedCurrentUser(userId);
        var handler = new SetRecipeRatingCommandHandler(ratings, user);
        var read = new GetRecipeByIdQueryHandler(recipes, user);
        var query = new GetRecipeByIdQuery { RecipeId = recipeId };
        Assert.Null((await read.HandleAsync(query))!.MyRating);

        var otherUser = new SetRecipeRatingCommandHandler(ratings, new FixedCurrentUser("other-owner"));
        Assert.False((await otherUser.HandleAsync(new SetRecipeRatingCommand(recipeId, 1))).Saved);
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
            () => handler.HandleAsync(new SetRecipeRatingCommand(recipeId, 6)));
        Assert.Empty(await db.RecipeRatings.Where(x => x.RecipeId == recipeId).ToListAsync());

        await using var secondScope = fixture.Factory.Services.CreateAsyncScope();
        var secondRatings = secondScope.ServiceProvider.GetRequiredService<IRecipeRatingRepository>();
        var secondHandler = new SetRecipeRatingCommandHandler(secondRatings, user);
        var firstRatings = await Task.WhenAll(
            handler.HandleAsync(new SetRecipeRatingCommand(recipeId, 2)),
            secondHandler.HandleAsync(new SetRecipeRatingCommand(recipeId, 3)));
        Assert.All(firstRatings, result => Assert.True(result.Saved));
        Assert.Single(await db.RecipeRatings.Where(x => x.RecipeId == recipeId).ToListAsync());

        Assert.True((await handler.HandleAsync(new SetRecipeRatingCommand(recipeId, 5))).Saved);
        Assert.Equal(5, (await read.HandleAsync(query))!.MyRating);
        Assert.Equal(updatedAt, (await recipes.GetByIdAsync(userId, recipeId))!.UpdatedAt);

        Assert.True((await handler.HandleAsync(new SetRecipeRatingCommand(recipeId, 0))).Saved);
        Assert.Null((await read.HandleAsync(query))!.MyRating);
        Assert.Empty(await db.RecipeRatings.Where(x => x.RecipeId == recipeId).ToListAsync());
    }
}
