using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Domain.Entities;
using RecipeLibrary.Domain.ValueObjects;
using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Testing;

public sealed class TestSeedData
{
    public Guid RecipeId { get; init; }
    public Guid GehaktCanonicalId { get; init; }
    public Guid TomatenCanonicalId { get; init; }
    public Guid ShoppingListGroupId { get; init; }
    public Guid ShoppingListId { get; init; }
}

public static class TestDataSeeder
{
    public const string LasagnaTitle = "Test Lasagna";

    public static async Task<TestSeedData> SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        return await SeedDatabaseAsync(db, ct);
    }

    public static async Task<TestSeedData> SeedWithConnectionStringAsync(string connectionString, CancellationToken ct = default)
    {
        var options = new DbContextOptionsBuilder<RecipeDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var db = new RecipeDbContext(options);
        await db.Database.MigrateAsync(ct);
        return await SeedDatabaseAsync(db, ct);
    }

    private static async Task<TestSeedData> SeedDatabaseAsync(RecipeDbContext db, CancellationToken ct)
    {
        if (await db.Recipes.AnyAsync(ct))
        {
            return await LoadExistingAsync(db, ct);
        }

        var now = DateTimeOffset.UtcNow;
        var gehaktId = Guid.NewGuid();
        var tomatenId = Guid.NewGuid();

        db.Ingredients.AddRange(
            new CanonicalIngredient
            {
                Id = gehaktId,
                CanonicalName = "Gehakt",
                NormalizedName = "gehakt",
                CreatedAt = now,
            },
            new CanonicalIngredient
            {
                Id = tomatenId,
                CanonicalName = "Tomaten",
                NormalizedName = "tomaten",
                CreatedAt = now,
            });

        var recipeId = Guid.NewGuid();
        var recipe = new Recipe
        {
            Id = recipeId,
            Title = new RecipeTitle(LasagnaTitle),
            Description = "Seeded recipe for automated tests.",
            PreparationMinutes = 30,
            CookingMinutes = 60,
            Category = RecipeCategory.Meat,
            CreatedAt = now,
            UpdatedAt = now,
            Ingredients =
            [
                new Ingredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipeId,
                    IngredientId = gehaktId,
                    Name = "Gehakt",
                    Preparation = "ruim",
                    Quantity = new Quantity(500),
                    Unit = Unit.Gram,
                },
                new Ingredient
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipeId,
                    IngredientId = tomatenId,
                    Name = "Tomaten",
                    Quantity = new Quantity(3),
                    Unit = Unit.Piece,
                },
            ],
            InstructionSteps =
            [
                new InstructionStep
                {
                    Id = Guid.NewGuid(),
                    RecipeId = recipeId,
                    StepNumber = 1,
                    Text = "Mix ingredients.",
                },
            ],
        };

        db.Recipes.Add(recipe);

        var groupId = Guid.NewGuid();
        var listId = Guid.NewGuid();
        db.ShoppingListGroups.Add(new ShoppingListGroup
        {
            Id = groupId,
            CreatedAt = now,
            UpdatedAt = now,
            Lists =
            [
                new ShoppingList
                {
                    Id = listId,
                    GroupId = groupId,
                    Name = "Boodschappenlijst 1",
                    StoreOrder = 1,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
            ],
        });

        await db.SaveChangesAsync(ct);

        return new TestSeedData
        {
            RecipeId = recipeId,
            GehaktCanonicalId = gehaktId,
            TomatenCanonicalId = tomatenId,
            ShoppingListGroupId = groupId,
            ShoppingListId = listId,
        };
    }

    private static async Task<TestSeedData> LoadExistingAsync(RecipeDbContext db, CancellationToken ct)
    {
        var recipes = await db.Recipes.ToListAsync(ct);
        var recipe = recipes.FirstOrDefault(r => r.Title.Value == LasagnaTitle) ?? recipes.First();
        var gehakt = await db.Ingredients.FirstAsync(i => i.NormalizedName == "gehakt", ct);
        var tomaten = await db.Ingredients.FirstAsync(i => i.NormalizedName == "tomaten", ct);
        var group = await db.ShoppingListGroups.Include(g => g.Lists).FirstAsync(ct);
        var list = group.Lists.OrderBy(l => l.StoreOrder).First();

        return new TestSeedData
        {
            RecipeId = recipe.Id,
            GehaktCanonicalId = gehakt.Id,
            TomatenCanonicalId = tomaten.Id,
            ShoppingListGroupId = group.Id,
            ShoppingListId = list.Id,
        };
    }
}
