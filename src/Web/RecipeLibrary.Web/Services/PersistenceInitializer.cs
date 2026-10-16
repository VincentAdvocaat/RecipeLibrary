using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Infrastructure.Identity;
using RecipeLibrary.Infrastructure.ContentModeration;

namespace RecipeLibrary.Web.Services;

public interface IPersistenceInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

public interface IPersistenceSchemaInitializer
{
    Task InitializeAsync(CancellationToken cancellationToken);
}

public sealed class PersistenceSchemaInitializer(IServiceScopeFactory scopeFactory) : IPersistenceSchemaInitializer
{
    public Task InitializeAsync(CancellationToken cancellationToken) =>
        PersistenceServiceRegistration.EnsurePersistenceMigratedAsync(scopeFactory, cancellationToken);
}

public sealed class PersistenceInitializer(
    IPersistenceSchemaInitializer schema,
    IdentitySeedUserHostedService seedUser,
    ContentModerationAdminSeedHostedService seedAdmins) : IPersistenceInitializer
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await schema.InitializeAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        // Development's optional account seed must run after migrations, without blocking HTTP startup.
        await seedUser.StartAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await seedAdmins.StartAsync(cancellationToken);
    }
}

public sealed class PersistenceWarmupOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
}
