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
    public async Task ApplyAsync(SeedEntry[] entries, string ownerId, string connectionString, CancellationToken ct)
    {
        var appliedMigrations = await db.Database.GetAppliedMigrationsAsync(ct);
        if (!appliedMigrations.Any(name => name.EndsWith("_AddRecipeRatings", StringComparison.Ordinal)))
        {
            throw new InvalidOperationException("Apply the AddRecipeRatings migration to the target database first.");
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
        var ratingsAdded = 0;
        foreach (var entry in entries)
        {
            var description = entry.Recipe.Description!.Trim();
            var matchingIds = await db.Recipes
                .Where(recipe => recipe.OwnerUserId == owner.Id && recipe.Description == description)
                .Select(recipe => recipe.Id)
                .Take(2)
                .ToListAsync(ct);
            if (matchingIds.Count > 1)
            {
                throw new InvalidOperationException($"{entry.SeedKey}: multiple recipes with the same source for this owner.");
            }

            var recipeId = matchingIds.SingleOrDefault();
            if (matchingIds.Count == 0)
            {
                var result = await commands.SendAsync<CreateRecipeCommand, CreateRecipeResult>(entry.Recipe, ct);
                recipeId = result.RecipeId;
                created++;
            }

            if (entry.Source.RatingStars is int stars && await AddRatingIfMissingAsync(recipeId, owner.Id, stars, ct))
            {
                ratingsAdded++;
            }

            db.ChangeTracker.Clear();
        }

        Console.WriteLine($"Recipes created: {created}, existing: {entries.Length - created}, personal ratings added: {ratingsAdded}.");
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
