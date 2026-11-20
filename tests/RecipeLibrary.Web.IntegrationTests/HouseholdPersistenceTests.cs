using System.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Pantry;
using RecipeLibrary.Application.UseCases.Pantry;
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
    public async Task ConcurrentHouseholdPantryUpserts_KeepOneItem()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RecipeDbContext>();
        var user = Guid.NewGuid().ToString();
        db.Users.Add(new ApplicationUser { Id = user });
        await db.SaveChangesAsync();
        var households = services.GetRequiredService<IHouseholdRepository>();
        Assert.True(await households.CreateAsync(user, "Concurrent pantry", default));
        var household = (await households.GetAsync(user, default))!;
        async Task<UpsertPantryItemResult> UpsertAsync()
        {
            await using var operationScope = fixture.Factory.Services.CreateAsyncScope();
            var provider = operationScope.ServiceProvider;
            var handler = new UpsertPantryItemCommandHandler(
                provider.GetRequiredService<IPantryRepository>(), provider.GetRequiredService<IShoppingListRepository>(),
                new FixedCurrentUser(user), new PantryIngredientMerger(new IngredientTextNormalizer()),
                provider.GetRequiredService<IUnitOfWork>(), provider.GetRequiredService<IHouseholdRepository>());
            return await handler.HandleAsync(new UpsertPantryItemCommand { HouseholdId = household.Id, DisplayName = "Salt" });
        }
        var results = await Task.WhenAll(UpsertAsync(), UpsertAsync());
        Assert.Equal(results[0].ItemId, results[1].ItemId);
        Assert.Single(await db.PantryItems.Where(item => item.OwnerUserId == PantryOwnerKey.ForHousehold(household.Id)).ToListAsync());
    }

    [Fact]
    public async Task SerializableUnitOfWork_RollsBackNewTrackedItemsAndCanRunAgain()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        var unit = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var owner = $"household:{Guid.NewGuid():D}";
        var id = Guid.NewGuid();
        var pending = new PantryItem { Id = Guid.NewGuid(), OwnerUserId = "pending:" + owner, DisplayName = "Pepper" };
        db.PantryItems.Add(pending);
        await Assert.ThrowsAsync<InvalidOperationException>(() => unit.ExecuteInSerializableTransactionAsync(async ct =>
        {
            Assert.Equal(IsolationLevel.Serializable, db.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel);
            db.PantryItems.Add(new PantryItem { Id = id, OwnerUserId = owner, DisplayName = "Salt" });
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Simulated operation failure");
        }));
        Assert.DoesNotContain(db.ChangeTracker.Entries<PantryItem>(), entry => entry.Entity.Id == id);
        Assert.Equal(EntityState.Added, db.Entry(pending).State);
        Assert.False(await db.PantryItems.AnyAsync(item => item.Id == pending.Id));
        Assert.False(await db.PantryItems.AnyAsync(item => item.Id == id));
        await unit.ExecuteInSerializableTransactionAsync(ct =>
        {
            db.PantryItems.Add(new PantryItem { Id = id, OwnerUserId = owner, DisplayName = "Salt" });
            return Task.CompletedTask;
        });
        Assert.Single(await db.PantryItems.Where(item => item.OwnerUserId == owner).ToListAsync());
        Assert.Equal(EntityState.Unchanged, db.Entry(pending).State);
    }

    [Fact]
    public async Task SerializableMembershipRead_BlocksConcurrentMemberRemovalUntilCommit()
    {
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<RecipeDbContext>();
        var manager = Guid.NewGuid().ToString();
        var member = Guid.NewGuid().ToString();
        db.Users.AddRange(new ApplicationUser { Id = manager }, new ApplicationUser { Id = member });
        await db.SaveChangesAsync();
        var households = services.GetRequiredService<IHouseholdRepository>();
        Assert.True(await households.CreateAsync(manager, "Transaction test", default));
        var household = (await households.GetAsync(manager, default))!;
        var token = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;
        Assert.True(await households.CreateInvitationAsync(manager, token, now.AddHours(1), default));
        Assert.Equal(JoinHouseholdResult.Joined, await households.JoinAsync(member, token, now, default));
        Task<bool>? removal = null;
        await using var otherScope = fixture.Factory.Services.CreateAsyncScope();
        var otherHouseholds = otherScope.ServiceProvider.GetRequiredService<IHouseholdRepository>();
        await services.GetRequiredService<IUnitOfWork>().ExecuteInSerializableTransactionAsync(async ct =>
        {
            Assert.True(await households.IsMemberAsync(member, household.Id, ct));
            removal = otherHouseholds.RemoveMemberAsync(manager, member, ct);
            await Task.Delay(200, ct);
            Assert.False(removal.IsCompleted);
            Assert.True(await households.IsMemberAsync(member, household.Id, ct));
        });
        Assert.True(await removal!.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.False(await households.IsMemberAsync(member, household.Id, default));
    }

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
