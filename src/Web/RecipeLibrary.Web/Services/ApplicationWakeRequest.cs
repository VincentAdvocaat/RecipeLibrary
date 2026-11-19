namespace RecipeLibrary.Web.Services;

public sealed record ApplicationWakeEvent(string AttemptId, string Reason, DateTimeOffset Timestamp,
    string CorrelationId, string Stage);

public interface IApplicationWakeObserver
{
    void Observe(ApplicationWakeEvent wakeEvent);
}

public sealed class NullApplicationWakeObserver : IApplicationWakeObserver
{
    public void Observe(ApplicationWakeEvent wakeEvent) { }
}

/// <summary>A process-wide, single-flight explicit request. Reading state never signals it.</summary>
public sealed class ApplicationWakeRequest(IEnumerable<IApplicationWakeObserver> observers,
    ILogger<ApplicationWakeRequest> logger)
{
    private readonly TaskCompletionSource<ApplicationWakeEvent> _requested = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public bool IsRequested => _requested.Task.IsCompletedSuccessfully;
    private long _lastActivityTicks;
    private readonly object _requestLock = new();
    public DateTimeOffset? LastActivity => Interlocked.Read(ref _lastActivityTicks) is var ticks && ticks != 0
        ? new DateTimeOffset(ticks, TimeSpan.Zero) : null;
    public Task<ApplicationWakeEvent> WaitAsync(CancellationToken ct) => _requested.Task.WaitAsync(ct);
    public void Request(string reason, string correlationId)
    {
        var attempt = new ApplicationWakeEvent(Guid.NewGuid().ToString("N"), reason,
            DateTimeOffset.UtcNow, correlationId, "Requested");
        Interlocked.Exchange(ref _lastActivityTicks, attempt.Timestamp.UtcTicks);
        lock (_requestLock)
        {
            if (_requested.Task.IsCompleted) return;
            Report(attempt);
            _requested.TrySetResult(attempt);
        }
    }
    public void Report(ApplicationWakeEvent attempt, string? stage = null)
    {
        var entry = stage is null ? attempt : attempt with { Stage = stage, Timestamp = DateTimeOffset.UtcNow };
        logger.LogInformation("Application wake {Stage}. Attempt={AttemptId} Reason={Reason} Correlation={CorrelationId}",
            entry.Stage, entry.AttemptId, entry.Reason, entry.CorrelationId);
        foreach (var observer in observers)
        {
            try { observer.Observe(entry); }
            catch (Exception ex) { logger.LogWarning(ex, "Application wake observer failed"); }
        }
    }
}
