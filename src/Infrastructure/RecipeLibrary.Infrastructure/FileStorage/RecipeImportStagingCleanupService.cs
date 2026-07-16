using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Infrastructure.FileStorage;

public sealed class RecipeImportStagingCleanupService(
    IServiceScopeFactory scopeFactory,
    ILogger<RecipeImportStagingCleanupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(Interval, stoppingToken);
                using var scope = scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IRecipeImportStagingStore>();
                var removed = await store.DeleteExpiredSessionsAsync(stoppingToken);
                if (removed > 0)
                {
                    logger.LogInformation("Removed {Count} expired recipe import staging session(s).", removed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to clean up expired recipe import staging sessions.");
            }
        }
    }
}
