using Microsoft.Extensions.Options;
using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Web.Demo;

public sealed class DemoCleanupService(IServiceScopeFactory scopes, IOptions<DemoOptions> options,
    TimeProvider time, ILogger<DemoCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                if (scope.ServiceProvider.GetRequiredService<IPersistenceReadiness>().IsReady)
                    await scope.ServiceProvider.GetRequiredService<DemoSessionService>().CleanupAsync(stoppingToken);
                else
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), time, stoppingToken);
                    continue;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Demo cleanup failed; will retry"); }
            await Task.Delay(TimeSpan.FromMinutes(options.Value.CleanupIntervalMinutes), time, stoppingToken);
        }
    }
}
