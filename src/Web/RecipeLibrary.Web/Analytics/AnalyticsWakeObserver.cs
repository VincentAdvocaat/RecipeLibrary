using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RecipeLibrary.Web.Services;

namespace RecipeLibrary.Web.Analytics;

public sealed class AnalyticsWakeObserver(TrafficRecorder recorder) : IApplicationWakeObserver
{
    public void Observe(ApplicationWakeEvent wakeEvent)
    {
        var kind = wakeEvent.Stage switch
        {
            "Requested" => "database-start-requested",
            "Ready" => "database-start-success",
            "Failed" => "database-start-failure",
            "ProbeStarted" => "database-probe",
            "ProbeFailed" => "database-probe-failure",
            _ => null
        };
        if (kind is not null) recorder.RecordLifecycle(kind, "user-start", wakeEvent.CorrelationId);
    }
}

/// <summary>Records EF connection boundaries without collecting SQL, connection strings or exception text.</summary>
public sealed class TrafficSqlConnectionInterceptor(TrafficRecorder recorder, IHttpContextAccessor accessor) : DbConnectionInterceptor
{
    private void Record(string kind, Exception? error = null)
    {
        var context = accessor.HttpContext;
        var reason = error is SqlException { Number: 42119 } ? "free-limit-exhausted"
            : context is null ? "background" : "web-request";
        recorder.RecordSqlConnection(kind, context, reason);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData) => Record("database-connection-open");
    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        Record("database-connection-open");
        return Task.CompletedTask;
    }
    public override void ConnectionFailed(DbConnection connection, ConnectionErrorEventData eventData) => Record("database-connection-failure", eventData.Exception);
    public override Task ConnectionFailedAsync(DbConnection connection, ConnectionErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Record("database-connection-failure", eventData.Exception);
        return Task.CompletedTask;
    }
}
