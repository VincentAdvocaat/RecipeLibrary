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
    public void Database_HasAppliedAllMigrations_AndRecipesTable()
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();

        Assert.True(db.Database.CanConnect());

        var defined = db.Database.GetMigrations().ToList();
        var applied = db.Database.GetAppliedMigrations().ToList();
        var pending = db.Database.GetPendingMigrations().ToList();

        Assert.NotEmpty(defined);
        Assert.Empty(pending);
        Assert.Equal(defined.OrderBy(x => x), applied.OrderBy(x => x));

        var connection = db.Database.GetDbConnection();
        connection.Open();
        try
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES
                WHERE TABLE_NAME = N'Recipes'
                """;
            var count = Convert.ToInt32(command.ExecuteScalar());
            Assert.Equal(1, count);
        }
        finally
        {
            connection.Close();
        }
    }
}
