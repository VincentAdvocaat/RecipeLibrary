using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Validation.AspNetCore;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace RecipeLibrary.Web.Auth;

public static class OpenIddictServiceCollectionExtensions
{
    public static IServiceCollection AddRecipeLibraryOpenIddict(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<RecipeLibrary.Infrastructure.Persistence.RecipeDbContext>();
            })
            .AddServer(options =>
            {
                options.SetTokenEndpointUris("connect/token")
                    .SetRevocationEndpointUris("connect/revoke");

                options.AllowPasswordFlow()
                    .AllowRefreshTokenFlow();

                options.RegisterScopes(
                    Scopes.Email,
                    Scopes.Profile,
                    Scopes.Roles,
                    OpenIddictAppConstants.ApiScope,
                    Scopes.OfflineAccess);

                options.DisableAccessTokenEncryption();

                ConfigureSigning(options, configuration, environment);

                var aspNetCoreBuilder = options.UseAspNetCore()
                    .EnableTokenEndpointPassthrough();

                if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
                {
                    aspNetCoreBuilder.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(options =>
            {
                options.UseLocalServer();
                options.UseAspNetCore();
            });

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = OpenIddictAppConstants.CookieOrBearerScheme;
                options.DefaultAuthenticateScheme = OpenIddictAppConstants.CookieOrBearerScheme;
                options.DefaultChallengeScheme = IdentityConstants.ApplicationScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, CookieOrBearerAuthenticationHandler>(
                OpenIddictAppConstants.CookieOrBearerScheme,
                displayName: null,
                _ => { });

        services.AddHostedService<OpenIddictClientSeedHostedService>();

        return services;
    }

    private static void ConfigureSigning(
        OpenIddictServerBuilder options,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var signingKey = configuration["OpenIddict:SigningKey"];
        if (!string.IsNullOrWhiteSpace(signingKey))
        {
            var keyBytes = Encoding.UTF8.GetBytes(signingKey);
            if (keyBytes.Length < 32)
            {
                throw new InvalidOperationException(
                    "OpenIddict:SigningKey must be at least 32 UTF-8 bytes (256 bits).");
            }

            var key = new SymmetricSecurityKey(keyBytes);
            options.AddSigningKey(key);
            options.AddEncryptionKey(key);
            return;
        }

        if (environment.IsDevelopment() || environment.IsEnvironment("Testing"))
        {
            options.AddEphemeralEncryptionKey()
                .AddEphemeralSigningKey();
            return;
        }

        throw new InvalidOperationException(
            "Missing OpenIddict:SigningKey. Set a stable 32+ character secret (e.g. Key Vault) for non-Development hosts.");
    }
}
