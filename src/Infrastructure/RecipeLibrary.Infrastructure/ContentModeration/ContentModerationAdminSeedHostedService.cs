using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Infrastructure.Identity;

namespace RecipeLibrary.Infrastructure.ContentModeration;

/// <summary>
/// Ensures the Admin role exists and assigns it to configured emails.
/// </summary>
public sealed class ContentModerationAdminSeedHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<ContentModerationOptions> options,
    ILogger<ContentModerationAdminSeedHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var emails = options.Value.AdminEmails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (emails.Length == 0)
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        if (!await roleManager.RoleExistsAsync(ContentModerationOptions.AdminRoleName))
        {
            var createRole = await roleManager.CreateAsync(new IdentityRole(ContentModerationOptions.AdminRoleName));
            if (!createRole.Succeeded)
            {
                logger.LogWarning(
                    "Failed to create Admin role: {Errors}",
                    string.Join("; ", createRole.Errors.Select(e => e.Description)));
                return;
            }
        }

        foreach (var email in emails)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                logger.LogDebug("Admin seed skipped: no user with email {Email}.", email);
                continue;
            }

            if (await userManager.IsInRoleAsync(user, ContentModerationOptions.AdminRoleName))
            {
                continue;
            }

            var add = await userManager.AddToRoleAsync(user, ContentModerationOptions.AdminRoleName);
            if (add.Succeeded)
            {
                logger.LogInformation("Granted Admin role to {Email}.", email);
            }
            else
            {
                logger.LogWarning(
                    "Failed to grant Admin role to {Email}: {Errors}",
                    email,
                    string.Join("; ", add.Errors.Select(e => e.Description)));
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
