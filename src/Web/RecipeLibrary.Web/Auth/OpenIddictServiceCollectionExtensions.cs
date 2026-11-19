using System.Security.Cryptography;
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
        var encryptionKey = configuration["OpenIddict:EncryptionKey"];
        if (string.IsNullOrWhiteSpace(signingKey) && string.IsNullOrWhiteSpace(encryptionKey)
            && (environment.IsDevelopment() || environment.IsEnvironment("Testing")))
        {
            options.AddEphemeralEncryptionKey()
                .AddEphemeralSigningKey();
            return;
        }

        options.AddSigningKey(ReadPrivateKey(signingKey, "OpenIddict:SigningKey"));
        options.AddEncryptionKey(ReadPrivateKey(encryptionKey, "OpenIddict:EncryptionKey"));
    }

    private static RsaSecurityKey ReadPrivateKey(string? pem, string setting)
    {
        if (string.IsNullOrWhiteSpace(pem))
        {
            throw new InvalidOperationException($"Missing {setting}. Configure a PEM-encoded RSA private key.");
        }

        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        var parameters = rsa.ExportParameters(includePrivateParameters: true);
        if (rsa.KeySize < 2048)
        {
            throw new InvalidOperationException($"{setting} must be at least 2048 bits.");
        }

        return new RsaSecurityKey(parameters);
    }
}
