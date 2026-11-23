using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Infrastructure.Persistence;

internal sealed class RecipeCollectionSeeder(
    RecipeDbContext db,
    ICommandBus commands,
    SeedOwner currentUser)
{
    public async Task ApplyAsync(ReadySeedEntry[] entries, string ownerId, string connectionString, CancellationToken ct)
    {
        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!appliedMigrations.Any(name => name.EndsWith("_AddRecipeRatings", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Apply the AddRecipeRatings migration to the target database first.");
        }

        if (!appliedMigrations.Any(name => name.EndsWith("_AddRecipeSourceUrl", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Apply the AddRecipeSourceUrl migration to the target database first.");
        }

        if (!appliedMigrations.Any(name => name.EndsWith("_AddRecipeCollectionSeedKey", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Apply the AddRecipeCollectionSeedKey migration to the target database first.");
        }

        var owner = await db.Users
            .Where(user => user.Id == ownerId)
            .Select(user => new { user.Id, user.UserName })
            .SingleOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("The owner ID does not match an existing Identity account.");
        currentUser.UserId = owner.Id;
        currentUser.UserName = owner.UserName;

        // A connection-scoped lock serializes seed runs for this owner without
        // interfering with the ingredient repository's own transactions.
        var lockSettings = new SqlConnectionStringBuilder(connectionString) { Pooling = false };
        await using var seedLock = new SqlConnection(lockSettings.ConnectionString);
        await seedLock.OpenAsync(ct);
        await AcquireOwnerLockAsync(seedLock, owner.Id, ct);

        var created = 0;
        var sourceLinksAdded = 0;
        var ratingsAdded = 0;
        foreach (var entry in entries)
        {
            var sourceUrl = entry.SourceUrl;
            var description = entry.MatchDescription;
            var personalRecipes = db.Recipes
                .Where(recipe => recipe.OwnerUserId == owner.Id && recipe.HouseholdId == null);
            var recipeId = await personalRecipes
                .Where(recipe => recipe.CollectionSeedKey == entry.SeedKey)
                .Select(recipe => recipe.Id)
                .SingleOrDefaultAsync(ct);

            if (recipeId == Guid.Empty)
            {
                var matchingIds = await personalRecipes
                    .Where(recipe => recipe.CollectionSeedKey == null
                        && ((sourceUrl != null && EF.Functions.Collate(recipe.SourceUrl!, "Latin1_General_100_BIN2") == sourceUrl)
                            || (description != null && EF.Functions.Collate(recipe.Description!, "Latin1_General_100_BIN2") == description)))
                    .Select(recipe => recipe.Id)
                    .Take(2)
                    .ToListAsync(ct);
                if (matchingIds.Count > 1)
                {
                    throw new InvalidOperationException($"{entry.SeedKey}: multiple untracked recipes with the same source for this owner.");
                }

                recipeId = matchingIds.SingleOrDefault();
                if (matchingIds.Count == 0)
                {
                    var result = await commands.SendAsync<CreateRecipeCommand, CreateRecipeResult>(entry.Recipe, ct);
                    recipeId = result.RecipeId;
                    created++;
                }

                // Track even legacy recipes whose source was manually changed. Once tracked,
                // future runs must preserve user edits and deliberately cleared source links.
                sourceLinksAdded += await personalRecipes
                    .Where(recipe => recipe.Id == recipeId && recipe.CollectionSeedKey == null
                        && recipe.SourceUrl == null && sourceUrl != null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(recipe => recipe.CollectionSeedKey, entry.SeedKey)
                        .SetProperty(recipe => recipe.SourceUrl, sourceUrl)
                        .SetProperty(recipe => recipe.Description,
                            recipe => description != null
                                && EF.Functions.Collate(recipe.Description!, "Latin1_General_100_BIN2") == description
                                    ? null : recipe.Description), ct);
                await personalRecipes
                    .Where(recipe => recipe.Id == recipeId && recipe.CollectionSeedKey == null)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(recipe => recipe.CollectionSeedKey, entry.SeedKey), ct);
            }

            if (entry.RatingStars is int stars && await AddRatingIfMissingAsync(recipeId, owner.Id, stars, ct))
            {
                ratingsAdded++;
            }

            db.ChangeTracker.Clear();
        }

        Console.WriteLine($"Recipes created: {created}, existing: {entries.Length - created}, source links added after creation: {sourceLinksAdded}, personal ratings added: {ratingsAdded}.");
    }

    private static async Task AcquireOwnerLockAsync(SqlConnection connection, string ownerId, CancellationToken ct)
    {
        var ownerHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(ownerId)));
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resourceName, @LockMode = 'Exclusive',
                @LockOwner = 'Session', @LockTimeout = 15000;
            SELECT @result;
            """;
        command.Parameters.Add("@resourceName", SqlDbType.NVarChar, 255).Value =
            "RecipeLibrary.SeedRecipeCollection:" + ownerHash;
        if (await command.ExecuteScalarAsync(ct) is not int result || result < 0)
        {
            throw new InvalidOperationException("Could not lock the collection seed for this owner.");
        }
    }

    private Task<bool> AddRatingIfMissingAsync(Guid recipeId, string ownerId, int stars, CancellationToken ct) =>
        db.Database.CreateExecutionStrategy().ExecuteAsync(async token =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            var alreadyRated = await db.RecipeRatings
                .AnyAsync(rating => rating.RecipeId == recipeId && rating.UserId == ownerId, token);
            if (!alreadyRated)
            {
                var result = await commands.SendAsync<SetRecipeRatingCommand, SetRecipeRatingResult>(
                    new SetRecipeRatingCommand(recipeId, stars), token);
                if (!result.Saved)
                {
                    throw new InvalidOperationException($"Could not rate recipe {recipeId} as the seed owner.");
                }
            }

            await transaction.CommitAsync(token);
            return !alreadyRated;
        }, ct);
}
