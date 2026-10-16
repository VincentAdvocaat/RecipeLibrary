using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Infrastructure.Identity;

namespace RecipeLibrary.Web.Services;

public interface IPersistenceInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

public sealed class PersistenceInitializer(
    IServiceScopeFactory scopeFactory,
    IdentitySeedUserHostedService seedUser) : IPersistenceInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await PersistenceServiceRegistration.EnsurePersistenceMigratedAsync(scopeFactory, cancellationToken);
        // Development's optional account seed must run after migrations, without blocking HTTP startup.
        await seedUser.StartAsync(cancellationToken);
    }
}

public sealed class PersistenceWarmupOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}
