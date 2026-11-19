using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.Identity;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Web.Demo;

public static class DemoRegistration
{
    public static void AddDemo(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DemoOptions>().Bind(configuration.GetSection("Demo"))
            .Validate(x => x.LifetimeMinutes > 0 && x.CleanupIntervalMinutes > 0
                && x.StartsPerIpPerHour > 0 && x.MaxActiveSessions > 0 && x.MaxResets >= 0
                && x.MaxRecipes >= 8 && x.MaxLists > 0 && x.MaxItemsPerList >= 7
                && x.MaxPantryItems >= 3 && x.MaxSaveAttempts > 0, "Invalid demo limits.").ValidateOnStart();
        services.AddScoped<DemoIdentity>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<DemoIdentity>());
        services.AddScoped<IDemoContext>(sp => sp.GetRequiredService<DemoIdentity>());
        services.AddScoped<CircuitHandler, DemoCircuitHandler>();
        services.AddScoped<DemoOperationRunner>();
        services.AddScoped<IDemoOperationRunner>(sp => sp.GetRequiredService<DemoOperationRunner>());
        services.AddScoped<DemoSessionService>();
        services.AddHostedService<DemoCleanupService>();
        services.ConfigureApplicationCookie(cookie =>
        {
            cookie.Events.OnValidatePrincipal = async context =>
            {
                if (context.Principal?.HasClaim(DemoIdentity.DemoClaim, "true") == true)
                {
                    // Demo validation occurs at the operation boundary and in middleware.
                    // SecurityStampValidator would replace the extra claims after 30 minutes.
                    context.ShouldRenew = false;
                }
                else await SecurityStampValidator.ValidatePrincipalAsync(context);
            };
        });
    }
}
