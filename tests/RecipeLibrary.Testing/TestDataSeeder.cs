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

    /// <summary>Owner user id assigned to seeded recipes (private library scope).</summary>
    public const string TestOwnerUserId = "test-owner-user-id";

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
        var gehaktId = await GetOrCreateCanonicalAsync(db, "Gehakt", "gehakt", ct);
        var tomatenId = await GetOrCreateCanonicalAsync(db, "Tomaten", "tomaten", ct);

        var recipeId = Guid.NewGuid();
        var recipe = new Recipe
        {
            Id = recipeId,
            OwnerUserId = TestOwnerUserId,
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
            OwnerUserId = TestOwnerUserId,
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

    /// <summary>
    /// Resolves an existing catalog row by normalized NL display name or alias (after curated seed),
    /// otherwise creates a canonical ingredient with an NL translation for tests.
    /// </summary>
    private static async Task<Guid> GetOrCreateCanonicalAsync(
        RecipeDbContext db,
        string canonicalName,
        string normalizedName,
        CancellationToken ct)
    {
        var existingId = await db.IngredientTranslations
            .AsNoTracking()
            .Where(x => x.LanguageCode == "nl" && x.NormalizedDisplayName == normalizedName)
            .Select(x => x.IngredientId)
            .FirstOrDefaultAsync(ct);
        if (existingId != Guid.Empty)
        {
            return existingId;
        }

        var viaAlias = await db.IngredientTranslationAliases
            .AsNoTracking()
            .Where(x => x.NormalizedAlias == normalizedName && x.Translation.LanguageCode == "nl")
            .Select(x => x.Translation.IngredientId)
            .FirstOrDefaultAsync(ct);
        if (viaAlias != Guid.Empty)
        {
            return viaAlias;
        }

        var id = Guid.NewGuid();
        var translationId = Guid.NewGuid();
        db.Ingredients.Add(new CanonicalIngredient
        {
            Id = id,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.IngredientTranslations.Add(new IngredientTranslation
        {
            Id = translationId,
            IngredientId = id,
            LanguageCode = "nl",
            DisplayName = canonicalName,
            NormalizedDisplayName = normalizedName,
        });
        await db.SaveChangesAsync(ct);
        return id;
    }

    private static async Task<TestSeedData> LoadExistingAsync(RecipeDbContext db, CancellationToken ct)
    {
        var recipes = await db.Recipes.ToListAsync(ct);
        var recipe = recipes.FirstOrDefault(r => r.Title.Value == LasagnaTitle) ?? recipes.First();
        var gehaktId = await ResolveCanonicalIdAsync(db, "gehakt", ct);
        var tomatenId = await ResolveCanonicalIdAsync(db, "tomaten", ct);
        var group = await db.ShoppingListGroups.Include(g => g.Lists).FirstAsync(ct);
        var list = group.Lists.OrderBy(l => l.StoreOrder).First();

        return new TestSeedData
        {
            RecipeId = recipe.Id,
            GehaktCanonicalId = gehaktId,
            TomatenCanonicalId = tomatenId,
            ShoppingListGroupId = group.Id,
            ShoppingListId = list.Id,
        };
    }

    private static async Task<Guid> ResolveCanonicalIdAsync(RecipeDbContext db, string normalized, CancellationToken ct)
    {
        var byName = await db.IngredientTranslations
            .AsNoTracking()
            .Where(x => x.LanguageCode == "nl" && x.NormalizedDisplayName == normalized)
            .Select(x => x.IngredientId)
            .FirstOrDefaultAsync(ct);
        if (byName != Guid.Empty)
        {
            return byName;
        }

        return await db.IngredientTranslationAliases
            .AsNoTracking()
            .Where(x => x.NormalizedAlias == normalized)
            .Select(x => x.Translation.IngredientId)
            .FirstAsync(ct);
    }
}
