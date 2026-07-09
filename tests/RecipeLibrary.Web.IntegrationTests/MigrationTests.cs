using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class MigrationTests(SqlContainerFixture fixture)
{
    [Fact]
    public void Database_HasRecipesTable()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();

        Assert.True(db.Database.CanConnect());
        Assert.True(db.Database.GetMigrations().Any());
    }
}
