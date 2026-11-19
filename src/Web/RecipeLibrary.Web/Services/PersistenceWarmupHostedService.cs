using RecipeLibrary.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace RecipeLibrary.Web.Services;

/// <summary>
/// Retries EF migrations and seed when Azure SQL is paused or otherwise temporarily unavailable.
/// Stops retrying after a deadline or on a non-transient failure.
/// </summary>
public sealed class PersistenceWarmupHostedService(
    IPersistenceInitializer initializer,
    IOptions<PersistenceWarmupOptions> options,
    IPersistenceReadiness readiness,
    ILogger<PersistenceWarmupHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (readiness.IsReady || readiness.HasPermanentlyFailed)
        {
            return;
        }

        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        deadline.CancelAfter(options.Value.Timeout);
        var warmupToken = deadline.Token;
        var startedAt = TimeProvider.System.GetUtcNow();
        logger.LogInformation("Application starting (persistence warmup)");

        var delay = InitialDelay;
        while (!warmupToken.IsCancellationRequested)
        {
            try
            {
                await initializer.InitializeAsync(warmupToken);
                warmupToken.ThrowIfCancellationRequested();
                readiness.MarkReady();
                var durationSeconds = (TimeProvider.System.GetUtcNow() - startedAt).TotalSeconds;
                logger.LogInformation(
                    "Application ready. StartupDurationSeconds={StartupDurationSeconds:0.###}",
                    durationSeconds);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (OperationCanceledException) when (deadline.IsCancellationRequested)
            {
                readiness.MarkPermanentlyFailed();
                logger.LogError("Application failed. Category=WarmupTimeout StartupDurationSeconds={Duration}",
                    (TimeProvider.System.GetUtcNow() - startedAt).TotalSeconds);
                return;
            }
            catch (Exception ex) when (!SqlTransientExceptionDetector.IsTransient(ex))
            {
                readiness.MarkPermanentlyFailed();
                var durationSeconds = (TimeProvider.System.GetUtcNow() - startedAt).TotalSeconds;
                logger.LogError(
                    ex,
                    "Application failed. Category={Category} StartupDurationSeconds={StartupDurationSeconds:0.###}",
                    ex.GetType().Name, durationSeconds);
                return;
            }
            catch (Exception ex)
            {
                var elapsed = TimeProvider.System.GetUtcNow() - startedAt;
                if (elapsed >= options.Value.Timeout)
                {
                    readiness.MarkPermanentlyFailed();
                    logger.LogError(
                        ex,
                        "Application failed. StartupDurationSeconds={StartupDurationSeconds:0.###}",
                        elapsed.TotalSeconds);
                    return;
                }

                logger.LogWarning(
                    ex,
                    "Database is not ready yet (e.g. Azure SQL auto-pause). Retrying in {DelaySeconds:0}s.",
                    delay.TotalSeconds);

                try
                {
                    await Task.Delay(delay, warmupToken);
                }
                catch (OperationCanceledException) when (warmupToken.IsCancellationRequested)
                {
                    if (!stoppingToken.IsCancellationRequested)
                    {
                        readiness.MarkPermanentlyFailed();
                        logger.LogError("Application failed. Category=WarmupTimeout");
                    }
                    return;
                }

                var nextSeconds = Math.Min(delay.TotalSeconds * 1.5, MaxDelay.TotalSeconds);
                delay = TimeSpan.FromSeconds(nextSeconds);
            }
        }
        if (!stoppingToken.IsCancellationRequested)
        {
            readiness.MarkPermanentlyFailed();
            logger.LogError("Application failed. Category=WarmupTimeout");
        }
    }
}
