using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RecipeLibrary.Infrastructure.Identity;

/// <summary>
/// Ensures a configured Development seed user exists; updates password when config changes.
/// </summary>
public sealed class IdentitySeedUserHostedService(
    IServiceScopeFactory scopeFactory,
    IHostEnvironment environment,
    IOptions<IdentitySeedUserOptions> options,
    ILogger<IdentitySeedUserHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.Email)
            || string.IsNullOrWhiteSpace(settings.UserName)
            || string.IsNullOrWhiteSpace(settings.Password))
        {
            logger.LogDebug("Identity seed user skipped: Identity:SeedUser is not fully configured.");
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var user = await userManager.FindByEmailAsync(settings.Email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = settings.UserName.Trim(),
                Email = settings.Email.Trim(),
                EmailConfirmed = true,
            };

            var createResult = await userManager.CreateAsync(user, settings.Password);
            if (!createResult.Succeeded)
            {
                logger.LogWarning(
                    "Failed to create Identity seed user: {Errors}",
                    string.Join("; ", createResult.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Created Identity seed user {UserName}.", user.UserName);
            return;
        }

        if (!await userManager.CheckPasswordAsync(user, settings.Password))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await userManager.ResetPasswordAsync(user, token, settings.Password);
            if (!resetResult.Succeeded)
            {
                logger.LogWarning(
                    "Failed to update Identity seed user password: {Errors}",
                    string.Join("; ", resetResult.Errors.Select(e => e.Description)));
                return;
            }

            logger.LogInformation("Updated Identity seed user password for {UserName}.", user.UserName);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
