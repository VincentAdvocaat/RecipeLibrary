using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;

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

        services.AddDbContext<RecipeDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                // Helpful defaults for transient Azure SQL failures.
                sql.EnableRetryOnFailure();
            }));

        services.AddScoped<IRecipeRepository, EfRecipeRepository>();
        return services;
    }

    public static void EnsurePersistenceMigrated(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        db.Database.Migrate();
    }
}

