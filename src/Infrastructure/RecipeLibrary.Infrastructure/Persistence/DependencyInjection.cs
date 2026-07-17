using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Infrastructure.Persistence.Seed;

namespace RecipeLibrary.Infrastructure.Persistence;

public static class PersistenceServiceRegistration
{
    /// <summary>
    /// Registers persistence services.
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddSingleton<IPersistenceReadiness, PersistenceReadiness>();

        services.AddDbContext<RecipeDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                // Helpful defaults for transient Azure SQL failures.
                sql.EnableRetryOnFailure();
            }));

        services.AddScoped<IRecipeRepository, EfRecipeRepository>();
        services.AddScoped<IIngredientRepository, EfIngredientRepository>();
        services.AddScoped<IShoppingListRepository, EfShoppingListRepository>();
        services.AddScoped<IPantryRepository, EfPantryRepository>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IngredientCatalogSeeder>();
        return services;
    }

    /// <summary>
    /// Applies EF Core migrations, then idempotently seeds the curated ingredient catalog.
    /// </summary>
    public static void EnsurePersistenceMigrated(this IServiceProvider services)
    {
        EnsurePersistenceMigratedAsync(services, CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Applies EF Core migrations, then idempotently seeds the curated ingredient catalog.
    /// </summary>
    public static async Task EnsurePersistenceMigratedAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        await MigrateAndSeedAsync(scope.ServiceProvider, cancellationToken);
    }

    /// <summary>
    /// Applies EF Core migrations using a new scope from <paramref name="scopeFactory"/>.
    /// </summary>
    public static async Task EnsurePersistenceMigratedAsync(
        IServiceScopeFactory scopeFactory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);
        using var scope = scopeFactory.CreateScope();
        await MigrateAndSeedAsync(scope.ServiceProvider, cancellationToken);
    }

    /// <summary>
    /// Tries to migrate and seed; on success marks <see cref="IPersistenceReadiness"/> ready.
    /// Returns false when the database is temporarily unavailable (e.g. Azure SQL auto-pause)
    /// or when a non-transient failure permanently stops warmup (see <paramref name="error"/>).
    /// </summary>
    public static bool TryEnsurePersistenceMigrated(this IServiceProvider services, out Exception? error)
    {
        error = null;
        var readiness = services.GetRequiredService<IPersistenceReadiness>();
        if (readiness.IsReady)
        {
            return true;
        }

        if (readiness.HasPermanentlyFailed)
        {
            return false;
        }

        try
        {
            EnsurePersistenceMigrated(services);
            readiness.MarkReady();
            return true;
        }
        catch (Exception ex) when (SqlTransientExceptionDetector.IsTransient(ex))
        {
            error = ex;
            return false;
        }
        catch (Exception ex)
        {
            // Keep the process up so the starting/failed page can surface misconfiguration
            // instead of crash-looping the Container App revision.
            error = ex;
            readiness.MarkPermanentlyFailed();
            return false;
        }
    }

    private static async Task MigrateAndSeedAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var db = scopedServices.GetRequiredService<RecipeDbContext>();
        await db.Database.MigrateAsync(cancellationToken);

        var seeder = scopedServices.GetRequiredService<IngredientCatalogSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
