using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Infrastructure.Persistence;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

public sealed class IngredientFindOrCreateTests
{
    [Fact]
    public async Task FindOrCreateAsync_ReusesExistingTranslation_OnSecondCall()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RecipeDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new RecipeDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repo = new EfIngredientRepository(db);
        var first = await repo.FindOrCreateAsync("nl", "tomaat", "tomaat", null, null);
        var second = await repo.FindOrCreateAsync("nl", "tomaat", "tomaat", null, null);

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await db.Ingredients.CountAsync());
        Assert.Equal(1, await db.IngredientTranslations.CountAsync());
    }
}
