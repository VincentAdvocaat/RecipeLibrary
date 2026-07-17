using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Infrastructure.Persistence;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

public sealed class IngredientSearchOrderingTests
{
    [Fact]
    public async Task SearchAsync_OrdersBeforeTake_IncludesPrefixMatchBeyondFirstPage()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RecipeDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new RecipeDbContext(options);
        await db.Database.EnsureCreatedAsync();

        // More than take*3 historical candidates so a premature Take would drop "gehakt".
        for (var i = 0; i < 40; i++)
        {
            await AddNlIngredientAsync(db, $"zebra-{i:00}", $"zebra {i:00}");
        }

        await AddNlIngredientAsync(db, "gehakt", "gehakt");
        await db.SaveChangesAsync();

        var repo = new EfIngredientRepository(db);
        var results = await repo.SearchAsync("ge", ["nl"], take: 10);

        Assert.Contains(
            results,
            x => x.Translations.Any(t =>
                t.LanguageCode == "nl"
                && t.NormalizedDisplayName.Equals("gehakt", StringComparison.Ordinal)));
    }

    private static async Task AddNlIngredientAsync(RecipeDbContext db, string normalized, string displayName)
    {
        var ingredient = new CanonicalIngredient
        {
            Id = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        ingredient.Translations.Add(new IngredientTranslation
        {
            Id = Guid.NewGuid(),
            IngredientId = ingredient.Id,
            LanguageCode = "nl",
            DisplayName = displayName,
            NormalizedDisplayName = normalized,
        });
        await db.Ingredients.AddAsync(ingredient);
    }
}
