using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Web.Services;

/// <summary>
/// Retries EF migrations and seed when Azure SQL is paused or otherwise temporarily unavailable.
/// </summary>
public sealed class PersistenceWarmupHostedService(
    IServiceScopeFactory scopeFactory,
    IPersistenceReadiness readiness,
    ILogger<PersistenceWarmupHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (readiness.IsReady)
        {
            return;
        }

        var delay = InitialDelay;
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PersistenceServiceRegistration.EnsurePersistenceMigratedAsync(scopeFactory, stoppingToken);
                readiness.MarkReady();
                logger.LogInformation("Database migrations and seed completed; persistence is ready.");
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Database is not ready yet (e.g. Azure SQL auto-pause). Retrying in {DelaySeconds:0}s.",
                    delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    return;
                }

                var nextSeconds = Math.Min(delay.TotalSeconds * 1.5, MaxDelay.TotalSeconds);
                delay = TimeSpan.FromSeconds(nextSeconds);
            }
        }
    }
}
