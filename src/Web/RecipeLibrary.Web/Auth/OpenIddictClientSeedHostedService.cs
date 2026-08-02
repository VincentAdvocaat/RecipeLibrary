using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace RecipeLibrary.Web.Auth;

/// <summary>
/// Ensures the first-party MAUI OpenIddict client exists after migrations.
/// </summary>
public sealed class OpenIddictClientSeedHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<OpenIddictClientSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

        if (await manager.FindByClientIdAsync(OpenIddictAppConstants.MauiClientId, cancellationToken) is not null)
        {
            return;
        }

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

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
