using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Infrastructure.Persistence.Seed;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class IngredientCatalogSeedStartupTests(SqlContainerFixture fixture)
{
    [Fact]
    public async Task EnsurePersistenceMigrated_SeedsCatalogOnStartup()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();

        Assert.True(await db.Ingredients.CountAsync() > 100);
        Assert.True(await db.Ingredients.AnyAsync(x => x.NormalizedName == "tomaat"));
        Assert.True(
            await db.Ingredients.AnyAsync(x => x.NormalizedName == "gehakt")
            || await db.IngredientAliases.AnyAsync(x => x.NormalizedAlias == "gehakt"));
    }

    [Fact]
    public async Task SeedAsync_SecondRun_IsIdempotent()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        var seeder = scope.ServiceProvider.GetRequiredService<IngredientCatalogSeeder>();

        var before = await db.Ingredients.CountAsync();
        var result = await seeder.SeedAsync();

        Assert.Equal(0, result.IngredientsInserted);
        Assert.Equal(before, await db.Ingredients.CountAsync());
    }
}
