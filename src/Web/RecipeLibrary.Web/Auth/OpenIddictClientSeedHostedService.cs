using OpenIddict.Abstractions;
using RecipeLibrary.Infrastructure.Persistence;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace RecipeLibrary.Web.Auth;

/// <summary>
/// Ensures the first-party MAUI OpenIddict client exists after persistence is ready.
/// Waits for warmup so Azure SQL auto-pause does not crash host startup; treats duplicate
/// client creation as success when multiple replicas race.
/// </summary>
public sealed class OpenIddictClientSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IPersistenceReadiness readiness,
    ILogger<OpenIddictClientSeedHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(500);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested
               && !readiness.IsReady
               && !readiness.HasPermanentlyFailed)
        {
            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }

        if (!readiness.IsReady)
        {
            logger.LogWarning(
                "Skipping OpenIddict client seed because persistence did not become ready.");
            return;
        }

        try
        {
            await SeedClientAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Never fail the host: token endpoint can still work after a manual/retry seed on next start.
            logger.LogWarning(
                ex,
                "OpenIddict client seed failed for {ClientId}; will retry on next process start.",
                OpenIddictAppConstants.MauiClientId);
        }
    }

    private async Task SeedClientAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        if (await manager.FindByClientIdAsync(OpenIddictAppConstants.MauiClientId, cancellationToken) is not null)
        {
            return;
        }

        try
        {
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = OpenIddictAppConstants.MauiClientId,
                DisplayName = "Recipe Library MAUI",
                ClientType = ClientTypes.Public,
                Permissions =
                {
                    Permissions.Endpoints.Token,
                    Permissions.Endpoints.Revocation,
                    Permissions.GrantTypes.Password,
                    Permissions.GrantTypes.RefreshToken,
                    Permissions.Prefixes.Scope + OpenIddictAppConstants.ApiScope,
                    Permissions.Prefixes.Scope + Scopes.OfflineAccess,
                },
            }, cancellationToken);

            logger.LogInformation("Seeded OpenIddict client {ClientId}.", OpenIddictAppConstants.MauiClientId);
        }
        catch (Exception ex) when (IsDuplicateClientConflict(ex))
        {
            logger.LogDebug(
                ex,
                "OpenIddict client {ClientId} already exists (replica race).",
                OpenIddictAppConstants.MauiClientId);
        }
    }

    private static bool IsDuplicateClientConflict(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            var message = current.Message;
            if (message.Contains("unique", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || message.Contains("ClientId", StringComparison.OrdinalIgnoreCase)
                    && message.Contains("already", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // OpenIddict validation for an existing client identifier.
            if (current.GetType().Name.Contains("Validation", StringComparison.OrdinalIgnoreCase)
                && message.Contains(OpenIddictAppConstants.MauiClientId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
