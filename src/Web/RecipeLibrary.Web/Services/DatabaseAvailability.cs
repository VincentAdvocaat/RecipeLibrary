using Microsoft.Data.SqlClient;
using RecipeLibrary.Infrastructure.Persistence;

namespace RecipeLibrary.Web.Services;

public interface IDatabaseProbe
{
    Task CheckAsync(CancellationToken cancellationToken);
}

public sealed class SqlDatabaseProbe(string connectionString) : IDatabaseProbe
{
    public async Task CheckAsync(CancellationToken cancellationToken)
    {
        // Do not inherit EF execution retries: each visitor-driven probe has a hard deadline.
        var settings = new SqlConnectionStringBuilder(connectionString)
        {
            ConnectTimeout = 5,
            ConnectRetryCount = 0
        };
        await using var connection = new SqlConnection(settings.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.CommandTimeout = 5;
        await command.ExecuteScalarAsync(cancellationToken);
    }
}

/// <summary>Visitor-driven availability, independent of the one-time migration lifecycle.</summary>
public sealed class DatabaseAvailability(
    IPersistenceReadiness startup,
    IDatabaseProbe probe,
    TimeProvider clock,
    ILogger<DatabaseAvailability> logger,
    ApplicationWakeRequest? wake = null)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _checkedAt = DateTimeOffset.MinValue;
    private PersistenceWarmupState _state = PersistenceWarmupState.Starting;

    public Task<PersistenceWarmupState> GetStateAsync(CancellationToken cancellationToken) =>
        Task.FromResult(startup.IsReady && _checkedAt == DateTimeOffset.MinValue ? startup.State :
            startup.IsReady ? _state : startup.State);

    public async Task<PersistenceWarmupState> ProbeForStartAsync(CancellationToken cancellationToken, string? correlationId = null)
    {
        if (!startup.IsReady) return startup.State;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (clock.GetUtcNow() - _checkedAt < TimeSpan.FromSeconds(5)) return _state;

            var attempt = new ApplicationWakeEvent(Guid.NewGuid().ToString("N"), "ExplicitApplicationStart",
                clock.GetUtcNow(), correlationId ?? "", "ProbeStarted");
            using var wakeContext = ApplicationWakeContext.Push(attempt);
            wake?.Report(attempt);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(7));
            try
            {
                logger.LogInformation("Database probe requested. Reason=ExplicitApplicationStart");
                // A disconnected visitor must not cancel the probe shared by other visitors.
                await probe.CheckAsync(deadline.Token);
                _state = PersistenceWarmupState.Ready;
                wake?.Report(attempt, "ProbeSucceeded");
            }
            catch (Exception ex)
            {
                _state = PersistenceWarmupState.Starting;
                wake?.Report(attempt with { Reason = ex is SqlException { Number: 42119 } ? "free-limit-exhausted" : "probe-failed" }, "ProbeFailed");
                logger.LogWarning(ex, "Database availability check failed. Category={Category}", ex.GetType().Name);
            }

            _checkedAt = clock.GetUtcNow();
            return _state;
        }
        finally
        {
            _gate.Release();
        }
    }
}
