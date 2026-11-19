using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;

namespace RecipeLibrary.Web.Analytics;

public static class TrafficAnalyticsExtensions
{
    // Register directly after forwarded headers, before readiness, Identity and demo middleware.
    public static IApplicationBuilder UseTrafficAnalytics(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        var path = context.Request.Path;
        var services = context.RequestServices;
        IResult? result = null;
        if (path.Equals("/api/analytics/visit", StringComparison.OrdinalIgnoreCase))
        {
            if (HttpMethods.IsPost(context.Request.Method))
                result = await VisitAsync(context, services.GetRequiredService<IConfiguration>(), services.GetRequiredService<TrafficRecorder>(), services.GetRequiredService<TrafficEndpointLimits>());
            else if (HttpMethods.IsOptions(context.Request.Method))
            {
                if (!VisitOriginAllowed(context, services.GetRequiredService<IConfiguration>())) result = Results.StatusCode(403);
                else
                {
                    AddCors(context);
                    context.Response.Headers.AccessControlAllowMethods = "POST";
                    context.Response.Headers.AccessControlAllowHeaders = "Content-Type";
                    result = Results.NoContent();
                }
            }
            else result = Results.StatusCode(405);
        }
        else if (path.Equals("/api/analytics/report", StringComparison.OrdinalIgnoreCase))
            result = HttpMethods.IsGet(context.Request.Method)
                ? await ReportAsync(context, services.GetRequiredService<IConfiguration>(), services.GetRequiredService<ITrafficStore>(), services.GetRequiredService<TrafficWriter>(), services.GetRequiredService<TrafficRecorder>(), services.GetRequiredService<TrafficEndpointLimits>())
                : Results.StatusCode(405);
        if (result is not null) { await result.ExecuteAsync(context); return; }
        context.Response.OnCompleted(() => { services.GetRequiredService<TrafficRecorder>().RecordRequest(context); return Task.CompletedTask; });
        await next(context);
    });
    public static IServiceCollection AddTrafficAnalytics(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<TrafficRecorder>();
        services.AddSingleton<ITrafficStore>(provider => string.IsNullOrWhiteSpace(configuration["Analytics:CosmosEndpoint"])
            ? new MemoryTrafficStore() : new CosmosTrafficStore(configuration));
        services.AddSingleton<TrafficWriter>();
        services.AddHostedService(provider => provider.GetRequiredService<TrafficWriter>());
        services.AddSingleton<TrafficEndpointLimits>();
        return services;
    }
    public static bool Authorized(string? configured, string? supplied)
    {
        if (string.IsNullOrWhiteSpace(configured) || configured.Length < 32 || string.IsNullOrEmpty(supplied) || supplied.Length > 512) return false;
        return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(configured)), SHA256.HashData(Encoding.UTF8.GetBytes(supplied)));
    }
    public static bool VisitOriginAllowed(HttpContext context, IConfiguration configuration)
    {
        var origin = context.Request.Headers.Origin.ToString();
        if (origin.Length == 0) return context.Request.Headers["Sec-Fetch-Site"] == "same-origin";
        if (origin != TrafficPrivacy.Origin(origin)) return false;
        return string.Equals(origin, $"{context.Request.Scheme}://{context.Request.Host}", StringComparison.OrdinalIgnoreCase) ||
            (configuration.GetSection("Welcome:AllowedOrigins").Get<string[]>() ?? []).Contains(origin, StringComparer.OrdinalIgnoreCase);
    }
    private static void AddCors(HttpContext context)
    {
        if (!string.IsNullOrEmpty(context.Request.Headers.Origin))
        {
            context.Response.Headers.AccessControlAllowOrigin = context.Request.Headers.Origin;
            context.Response.Headers.Vary = "Origin";
        }
    }
    private static async Task<IResult> VisitAsync(HttpContext context, IConfiguration config, TrafficRecorder recorder, TrafficEndpointLimits limits)
    {
        using var lease = limits.Visits.AttemptAcquire();
        if (!lease.IsAcquired) return Results.StatusCode(429);
        if (!VisitOriginAllowed(context, config)) return Results.StatusCode(403);
        AddCors(context);
        if (context.Request.ContentLength > 2048) return Results.StatusCode(413);
        if (!context.Request.HasJsonContentType()) return Results.StatusCode(415);
        // Read a fixed buffer even for chunked bodies. Never ask the JSON binder to buffer an unlimited body.
        var buffer = new byte[2049];
        var count = 0;
        try
        {
            while (count < buffer.Length)
            {
                var read = await context.Request.Body.ReadAsync(buffer.AsMemory(count), context.RequestAborted);
                if (read == 0) break;
                count += read;
            }
            if (count > 2048) return Results.StatusCode(413);
            using var json = JsonDocument.Parse(buffer.AsMemory(0, count), new JsonDocumentOptions { MaxDepth = 4 });
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("event", out var eventValue) || eventValue.ValueKind != JsonValueKind.String) return Results.BadRequest();
            var kind = eventValue.GetString();
            if (kind is not ("landing" or "browser" or "open-app")) return Results.BadRequest();
            string? Read(string key) => root.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var page = TrafficPrivacy.Path(Read("page"));
            if (!TrafficPrivacy.Excluded(page)) recorder.RecordEvent(kind, context, page, Read("referrer"));
            return Results.Accepted();
        }
        catch (JsonException) { return Results.BadRequest(); }
        catch (IOException) { return Results.BadRequest(); }
    }
    private static async Task<IResult> ReportAsync(HttpContext context, IConfiguration config, ITrafficStore store, TrafficWriter writer, TrafficRecorder recorder, TrafficEndpointLimits limits)
    {
        context.Response.Headers.CacheControl = "no-store";
        using var lease = limits.Reports.AttemptAcquire();
        if (!lease.IsAcquired) return Results.StatusCode(429);
        if (!Authorized(config["Analytics:AdminKey"], context.Request.Headers["X-Analytics-Key"])) return Results.Unauthorized();
        var days = int.TryParse(context.Request.Query["days"], out var requested) ? Math.Clamp(requested, 1, 31) : 7;
        var now = DateTimeOffset.UtcNow;
        IReadOnlyList<TrafficHour> hours = [];
        var available = writer.Available;
        string? error = writer.Error;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try { hours = await store.ReadAsync(now.AddDays(-days), timeout.Token); available = true; }
        catch (Exception) when (!context.RequestAborted.IsCancellationRequested) { available = false; error = "Analytics storage unavailable"; }
        var detailBudget = 200;
        var reportHours = hours.OrderByDescending(h => h.Hour).Select(h =>
        {
            var details = h.Events.OrderByDescending(e => e.At).Take(detailBudget).ToArray();
            detailBudget -= details.Length;
            Dictionary<string, long> Top(Dictionary<string, long> values) => values.OrderByDescending(p => p.Value).Take(12).ToDictionary();
            return new { hour = h.Hour, counts = h.Counts, events = details, paths = Top(h.Paths), referrers = Top(h.Referrers), bots = Top(h.Bots), droppedDetails = h.DroppedDetails };
        }).OrderBy(h => h.hour).ToArray();
        return Results.Json(new
        {
            generatedAt = now,
            persistence = new { configured = store.Configured, available, lastSuccess = writer.LastSuccess, error, buffered = recorder.Buffered, dropped = recorder.Dropped },
            hours = reportHours,
            limits = new { retentionDays = 31, detailsPerHour = 200, reportDetails = 200, reportDimensionsPerHour = 12, dimensionsPerHour = 100, channelCapacity = 512, browserClaimsVerified = false }
        });
    }
}

public sealed class TrafficEndpointLimits : IDisposable
{
    public FixedWindowRateLimiter Visits { get; } = new(new FixedWindowRateLimiterOptions
    { PermitLimit = 120, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
    public FixedWindowRateLimiter Reports { get; } = new(new FixedWindowRateLimiterOptions
    { PermitLimit = 6, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
    public void Dispose() { Visits.Dispose(); Reports.Dispose(); }
}
