using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Infrastructure.Persistence.Seed;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

public sealed class IngredientCatalogSeederTests
{
    [Fact]
    public void LoadCatalog_ContainsDutchAndEnglishTomato()
    {
        var catalog = IngredientCatalogSeeder.LoadCatalog();

        Assert.True(catalog.Ingredients.Count > 100);
        var tomato = Assert.Single(catalog.Ingredients, x => x.Id == "tomato");
        Assert.Contains("tomaat", tomato.Names.Nl, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("tomato", tomato.Names.En, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SeedAsync_InsertsTranslations_AndIsIdempotent()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RecipeDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new RecipeDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var seeder = new IngredientCatalogSeeder(
            db,
            new IngredientTextNormalizer(),
            NullLogger<IngredientCatalogSeeder>.Instance);

        var first = await seeder.SeedAsync();
        Assert.True(first.IngredientsInserted > 100);
        Assert.True(first.TranslationsInserted > 100);
        Assert.True(first.AliasesInserted > 0);

        Assert.True(await db.Ingredients.AnyAsync(x => x.CatalogKey == "tomato"));
        Assert.True(await db.IngredientTranslations.AnyAsync(x =>
            x.LanguageCode == "nl" && x.NormalizedDisplayName == "tomaat"));
        Assert.True(await db.IngredientTranslations.AnyAsync(x =>
            x.LanguageCode == "nl" && x.NormalizedDisplayName == "gehakt"));
        Assert.True(await db.IngredientTranslations.AnyAsync(x =>
            x.LanguageCode == "en" && x.NormalizedDisplayName == "tomato"));
        Assert.True(await db.IngredientTranslationAliases.AnyAsync(x => x.NormalizedAlias == "tomaten"));

        var countAfterFirst = await db.Ingredients.CountAsync();
        var translationCountAfterFirst = await db.IngredientTranslations.CountAsync();
        var aliasCountAfterFirst = await db.IngredientTranslationAliases.CountAsync();

        var second = await seeder.SeedAsync();
        Assert.Equal(0, second.IngredientsInserted);
        Assert.Equal(0, second.TranslationsInserted);
        Assert.Equal(0, second.AliasesInserted);
        Assert.Equal(countAfterFirst, await db.Ingredients.CountAsync());
        Assert.Equal(translationCountAfterFirst, await db.IngredientTranslations.CountAsync());
        Assert.Equal(aliasCountAfterFirst, await db.IngredientTranslationAliases.CountAsync());
    }
}
