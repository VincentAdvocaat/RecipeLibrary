using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Web.Analytics;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

public sealed class AnalyticsTests
{
    private static IConfiguration Config(params (string Key, string Value)[] pairs) => new ConfigurationBuilder().AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value))).Build();
    [Fact]
    public void Sanitization_removes_queries_guids_credentials_and_email()
    {
        Assert.Equal("/recipes/:id", TrafficPrivacy.Path("/recipes/12345678-1234-1234-1234-123456789abc?email=private@example.com"));
        Assert.Equal("/:id", TrafficPrivacy.Path("/private@example.com"));
        Assert.Equal("/:id", TrafficPrivacy.Path("/private%2540example.com"));
        Assert.Equal("https://example.com", TrafficPrivacy.Origin("https://example.com/private?secret=1"));
        Assert.Empty(TrafficPrivacy.Origin("https://user:password@example.com/"));
        Assert.Empty(TrafficPrivacy.Origin("javascript:alert(1)"));
    }
    [Fact]
    public void Daily_pseudonym_is_bounded_and_rotates()
    {
        var now = DateTimeOffset.UtcNow;
        var secret = new string('k', 32);
        var a = TrafficPrivacy.Visitor("192.0.2.1", secret, now);
        Assert.Equal(12, a.Length);
        Assert.Equal(a, TrafficPrivacy.Visitor("192.0.2.1", secret, now));
        Assert.NotEqual(a, TrafficPrivacy.Visitor("192.0.2.1", secret, now.AddDays(1)));
        Assert.Empty(TrafficPrivacy.Visitor("192.0.2.1", "short", now));
    }
    [Theory]
    [InlineData(null, "key")]
    [InlineData("short", "short")]
    [InlineData("12345678901234567890123456789012", null)]
    [InlineData("12345678901234567890123456789012", "wrong")]
    public void Authentication_fails_closed(string? configured, string? supplied) => Assert.False(TrafficAnalyticsExtensions.Authorized(configured, supplied));
    [Fact]
    public void Authentication_accepts_exact_valid_key() => Assert.True(TrafficAnalyticsExtensions.Authorized(new string('k', 32), new string('k', 32)));
    [Fact]
    public void Origin_gate_accepts_only_same_origin_or_configured_welcome()
    {
        var ctx = new DefaultHttpContext(); ctx.Request.Scheme = "https"; ctx.Request.Host = new("app.example");
        var config = Config(("Welcome:AllowedOrigins:0", "https://welcome.example"));
        ctx.Request.Headers.Origin = "https://evil.example";
        Assert.False(TrafficAnalyticsExtensions.VisitOriginAllowed(ctx, config));
        ctx.Request.Headers.Origin = "https://welcome.example";
        Assert.True(TrafficAnalyticsExtensions.VisitOriginAllowed(ctx, config));
        ctx.Request.Headers.Origin = "https://app.example";
        Assert.True(TrafficAnalyticsExtensions.VisitOriginAllowed(ctx, config));
        ctx.Request.Headers.Origin = "null";
        Assert.False(TrafficAnalyticsExtensions.VisitOriginAllowed(ctx, config));
    }
    [Fact]
    public void Merge_is_idempotent_bounded_and_retention_does_not_slide()
    {
        var now = DateTimeOffset.UtcNow;
        var delta = TrafficHour.Create(now);
        for (var i = 0; i < 1000; i++) delta.Record(new(now, "request", "/" + i, Referrer: "https://" + i, Bot: i.ToString()));
        Assert.Equal(200, delta.Events.Count); Assert.Equal(800, delta.DroppedDetails);
        Assert.True(delta.Paths.Count <= 100); Assert.True(delta.Referrers.Count <= 100); Assert.True(delta.Bots.Count <= 100);
        var stored = TrafficHour.Create(now);
        Assert.True(stored.Merge(delta, "a", 1, now));
        var ttl = stored.ttl;
        Assert.False(stored.Merge(delta, "a", 1, now.AddHours(1)));
        Assert.True(stored.Merge(delta, "b", 1, now.AddHours(1)));
        Assert.Equal(2000, stored.Counts["request"]);
        Assert.Equal(200, stored.Events.Count);
        Assert.Equal(ttl - 3600, stored.ttl);
        Assert.False(stored.Merge(delta, "b", 2, now.AddDays(32)));
    }
    [Fact]
    public async Task Recorder_bounds_queue_and_persists_only_safe_fields()
    {
        var recorder = new TrafficRecorder(Config(("Analytics:AdminKey", new string('k', 32))));
        var ctx = new DefaultHttpContext(); ctx.Request.Path = "/login"; ctx.Request.QueryString = new("?email=private@example.com");
        ctx.Connection.RemoteIpAddress = IPAddress.Parse("192.0.2.1"); ctx.Request.Headers.Authorization = "Bearer SECRET";
        ctx.Request.Headers.Cookie = "private=SECRET";
        for (var i = 0; i < 600; i++) recorder.RecordRequest(ctx);
        Assert.Equal(512, recorder.Buffered); Assert.Equal(88, recorder.Dropped);
        var store = new MemoryTrafficStore(); var writer = new TrafficWriter(recorder, store);
        await writer.FlushAsync(default);
        var hour = Assert.Single(await store.ReadAsync(DateTimeOffset.UtcNow.AddDays(-1), default));
        var json = JsonSerializer.Serialize(hour);
        Assert.DoesNotContain("SECRET", json); Assert.DoesNotContain("192.0.2.1", json); Assert.DoesNotContain("private@example.com", json);
        Assert.Equal(0, recorder.Buffered); Assert.Equal(512, hour.Counts["request"]);
    }
    [Fact]
    public async Task Failed_store_does_not_throw_into_recording_pipeline()
    {
        var recorder = new TrafficRecorder(Config()); var writer = new TrafficWriter(recorder, new FailedStore());
        recorder.RecordLifecycle("database-start-failure", "password=SECRET");
        await writer.FlushAsync(default);
        Assert.False(writer.Available); Assert.Equal(1, recorder.Dropped); Assert.Equal(0, recorder.Buffered);
    }
    [Theory]
    [InlineData("/api/analytics/report", "GET", 401)]
    [InlineData("/api/analytics/visit", "POST", 403)]
    public async Task Analytics_branch_never_reaches_identity_or_sql(string path, string method, int expected)
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddOptions();
        var config = Config(); services.AddSingleton<IConfiguration>(config); services.AddTrafficAnalytics(config);
        await using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);
        app.UseTrafficAnalytics();
        app.Run(_ => throw new InvalidOperationException("Identity/SQL middleware must not run"));
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = path; context.Request.Method = method;
        await app.Build()(context);
        Assert.Equal(expected, context.Response.StatusCode);
    }
    [Fact]
    public async Task Chunked_visit_body_is_bounded_before_json_parsing()
    {
        var services = new ServiceCollection(); services.AddLogging(); services.AddOptions();
        var config = Config(); services.AddSingleton<IConfiguration>(config); services.AddTrafficAnalytics(config);
        await using var provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider); app.UseTrafficAnalytics();
        var context = new DefaultHttpContext { RequestServices = provider };
        context.Request.Path = "/api/analytics/visit"; context.Request.Method = "POST";
        context.Request.Scheme = "https"; context.Request.Host = new("example.com");
        context.Request.Headers.Origin = "https://example.com";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(new byte[10000]);
        await app.Build()(context);
        Assert.Equal(413, context.Response.StatusCode);
        Assert.Equal(2049, context.Request.Body.Position);
    }
    [Fact]
    public async Task Ambiguous_write_retry_does_not_double_count()
    {
        var recorder = new TrafficRecorder(Config()); var store = new AmbiguousStore();
        var writer = new TrafficWriter(recorder, store);
        recorder.RecordLifecycle("database-probe", "probe");
        await writer.FlushAsync(default);
        var hour = Assert.Single(await store.ReadAsync(DateTimeOffset.UtcNow.AddDays(-1), default));
        Assert.Equal(1, hour.Counts["database-probe"]);
        Assert.True(writer.Available);
    }
    [Fact]
    public async Task Invalid_keys_do_not_exhaust_the_administrator_report_limit()
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddOptions();
        var config = Config(("Analytics:AdminKey", new string('k', 32)));
        services.AddSingleton<IConfiguration>(config); services.AddTrafficAnalytics(config);
        await using var provider = services.BuildServiceProvider();
        var builder = new ApplicationBuilder(provider);
        builder.UseTrafficAnalytics();
        var app = builder.Build();
        for (var i = 0; i < 10; i++)
        {
            var context = new DefaultHttpContext { RequestServices = provider };
            context.Request.Path = "/api/analytics/report"; context.Request.Method = "GET";
            await app(context);
            Assert.Equal(401, context.Response.StatusCode);
        }
        var valid = new DefaultHttpContext { RequestServices = provider };
        valid.Response.Body = new MemoryStream();
        valid.Request.Path = "/api/analytics/report"; valid.Request.Method = "GET";
        valid.Request.Headers["X-Analytics-Key"] = new string('k', 32);
        await app(valid);
        Assert.Equal(200, valid.Response.StatusCode);
    }

    [Fact]
    public async Task Wake_observer_correlates_explicit_starts_and_failures_without_private_error_text()
    {
        var recorder = new TrafficRecorder(Config());
        var observer = new AnalyticsWakeObserver(recorder);
        var attempt = new RecipeLibrary.Web.Services.ApplicationWakeEvent("attempt", "ExplicitApplicationStart", DateTimeOffset.UtcNow, "request-123", "Requested");
        observer.Observe(attempt);
        observer.Observe(attempt with { Stage = "Failed" });
        var store = new MemoryTrafficStore();
        await new TrafficWriter(recorder, store).FlushAsync(default);
        var hour = Assert.Single(await store.ReadAsync(DateTimeOffset.UtcNow.AddDays(-1), default));
        Assert.Equal(1, hour.Counts["database-start-requested"]);
        Assert.Equal(1, hour.Counts["database-start-failure"]);
        Assert.All(hour.Events, e => { Assert.Equal("user-start", e.Reason); Assert.Equal("request-123", e.RequestId); });
    }

    [Fact]
    public async Task Hosted_initialization_SQL_events_keep_the_explicit_request_and_attempt()
    {
        var recorder = new TrafficRecorder(Config());
        var observer = new AnalyticsWakeObserver(recorder);
        var wake = new RecipeLibrary.Web.Services.ApplicationWakeRequest([observer], Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipeLibrary.Web.Services.ApplicationWakeRequest>.Instance);
        var readiness = new RecipeLibrary.Infrastructure.Persistence.PersistenceReadiness();
        var interceptor = new TrafficSqlConnectionInterceptor(recorder, new HttpContextAccessor());
        using var service = new RecipeLibrary.Web.Services.PersistenceWarmupHostedService(new CorrelationInitializer(interceptor),
            Microsoft.Extensions.Options.Options.Create(new RecipeLibrary.Web.Services.PersistenceWarmupOptions()), readiness,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipeLibrary.Web.Services.PersistenceWarmupHostedService>.Instance, wake);
        await service.StartAsync(default);
        wake.Request("ExplicitApplicationStart", "original-http-request");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!readiness.HasPermanentlyFailed) await Task.Delay(10, deadline.Token);
        var store = new MemoryTrafficStore();
        await new TrafficWriter(recorder, store).FlushAsync(default);
        var events = Assert.Single(await store.ReadAsync(DateTimeOffset.UtcNow.AddHours(-1), default)).Events;
        Assert.Contains(events, e => e.Kind == "database-connection-open");
        Assert.Contains(events, e => e.Kind == "database-start-failure");
        Assert.All(events, e => Assert.Equal("original-http-request", e.RequestId));
        Assert.NotEmpty(Assert.Single(events.Select(e => e.AttemptId).Distinct()));
        Assert.Null(RecipeLibrary.Web.Services.ApplicationWakeContext.Current);
        await service.StopAsync(default);
    }

    [Fact]
    public async Task Resume_probe_keeps_HTTP_correlation_and_reports_failure_category()
    {
        var recorder = new TrafficRecorder(Config());
        var observer = new AnalyticsWakeObserver(recorder);
        var wake = new RecipeLibrary.Web.Services.ApplicationWakeRequest([observer], Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipeLibrary.Web.Services.ApplicationWakeRequest>.Instance);
        var readiness = new RecipeLibrary.Infrastructure.Persistence.PersistenceReadiness();
        readiness.MarkReady();
        var probe = new CorrelationProbe();
        var availability = new RecipeLibrary.Web.Services.DatabaseAvailability(readiness, probe, TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<RecipeLibrary.Web.Services.DatabaseAvailability>.Instance, wake);
        await availability.ProbeForStartAsync(default, "resume-http-request");
        Assert.NotNull(probe.Attempt);
        var store = new MemoryTrafficStore();
        await new TrafficWriter(recorder, store).FlushAsync(default);
        var events = Assert.Single(await store.ReadAsync(DateTimeOffset.UtcNow.AddHours(-1), default)).Events;
        Assert.All(events, e => { Assert.Equal("resume-http-request", e.RequestId); Assert.Equal(probe.Attempt.AttemptId, e.AttemptId); });
        Assert.Contains(events, e => e.Kind == "database-probe-failure" && e.Reason == "probe-failed");
        Assert.Null(RecipeLibrary.Web.Services.ApplicationWakeContext.Current);
    }

    private sealed class CorrelationInitializer(TrafficSqlConnectionInterceptor interceptor) : RecipeLibrary.Web.Services.IPersistenceInitializer
    {
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            using var connection = new Microsoft.Data.SqlClient.SqlConnection();
            await interceptor.ConnectionOpenedAsync(connection, null!, cancellationToken);
            throw new InvalidOperationException("Initializer unavailable");
        }
    }
    private sealed class CorrelationProbe : RecipeLibrary.Web.Services.IDatabaseProbe
    {
        public RecipeLibrary.Web.Services.ApplicationWakeEvent? Attempt;
        public Task CheckAsync(CancellationToken cancellationToken)
        {
            Attempt = RecipeLibrary.Web.Services.ApplicationWakeContext.Current;
            throw new InvalidOperationException("Probe unavailable");
        }
    }

    private sealed class AmbiguousStore : ITrafficStore
    {
        private readonly MemoryTrafficStore memory = new(); private bool first = true;
        public bool Configured => true;
        public async Task MergeAsync(TrafficHour delta, string writer, long sequence, CancellationToken cancellationToken)
        {
            await memory.MergeAsync(delta, writer, sequence, cancellationToken);
            if (first) { first = false; throw new IOException("Response lost after commit"); }
        }
        public Task<IReadOnlyList<TrafficHour>> ReadAsync(DateTimeOffset from, CancellationToken cancellationToken) => memory.ReadAsync(from, cancellationToken);
    }
    private sealed class FailedStore : ITrafficStore
    {
        public bool Configured => true;
        public Task MergeAsync(TrafficHour delta, string writer, long sequence, CancellationToken cancellationToken) => throw new IOException("SECRET");
        public Task<IReadOnlyList<TrafficHour>> ReadAsync(DateTimeOffset from, CancellationToken cancellationToken) => throw new IOException("SECRET");
    }
}
