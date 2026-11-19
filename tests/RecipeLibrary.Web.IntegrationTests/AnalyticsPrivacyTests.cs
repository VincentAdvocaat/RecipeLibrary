using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Web.Analytics;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

public sealed class AnalyticsPrivacyTests
{
    [Fact]
    public async Task Optional_measurements_never_read_the_remote_IP_or_create_a_visitor_identifier()
    {
        var recorder = new TrafficRecorder();
        var context = Context();
        context.Features.Set<IHttpConnectionFeature>(new ForbiddenConnection());
        recorder.RecordRequest(context);
        recorder.RecordEvent("browser", context);
        var store = new MemoryTrafficStore();
        await new TrafficWriter(recorder, store).FlushAsync(default);
        var hour = Assert.Single(await store.ReadAsync(DateTimeOffset.UtcNow.AddHours(-1), default));
        Assert.Equal(2, hour.Events.Count);
        Assert.All(hour.Events, item => Assert.Empty(item.Visitor));
    }

    [Theory]
    [InlineData("DNT", "1")]
    [InlineData("Sec-GPC", "1")]
    [InlineData("Cookie", "RecipeLibrary.Analytics=off")]
    public async Task Opt_out_stops_request_and_browser_measurements_but_preserves_minimal_SQL_diagnostics(string header, string value)
    {
        var recorder = new TrafficRecorder();
        var context = Context(); context.Request.Headers[header] = value;
        context.Request.Headers.UserAgent = "Private browser";
        context.Request.Headers.Referer = "https://source.example/private";
        context.Features.Set<IHttpConnectionFeature>(new ForbiddenConnection());
        recorder.RecordRequest(context);
        recorder.RecordEvent("landing", context);
        recorder.RecordEvent("browser", context);
        recorder.RecordEvent("open-app", context);
        Assert.Equal(0, recorder.Buffered);
        recorder.RecordLifecycle("database-start-requested", "user-start", context.TraceIdentifier, "attempt");
        recorder.RecordSqlConnection("database-connection-failure", context, "timeout");
        var store = new MemoryTrafficStore();
        await new TrafficWriter(recorder, store).FlushAsync(default);
        var hour = Assert.Single(await store.ReadAsync(DateTimeOffset.UtcNow.AddHours(-1), default));
        Assert.Equal(2, hour.Events.Count);
        Assert.All(hour.Events, item =>
        {
            Assert.StartsWith("database-", item.Kind);
            Assert.Equal("correlated-request", item.RequestId);
            Assert.Empty(item.Visitor); Assert.Empty(item.Browser); Assert.Empty(item.Bot); Assert.Empty(item.Referrer);
        });
    }

    [Theory]
    [InlineData("DNT", "1")]
    [InlineData("Sec-GPC", "1")]
    [InlineData("Cookie", "RecipeLibrary.Analytics=off")]
    public async Task Visit_endpoint_ignores_opted_out_payloads(string header, string value)
    {
        await using var provider = Services();
        var app = new ApplicationBuilder(provider); app.UseTrafficAnalytics();
        var context = Context(provider); context.Request.Path = "/api/analytics/visit"; context.Request.Method = "POST";
        context.Request.Headers[header] = value;
        context.Request.Headers.Origin = "https://app.example";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream("{\"event\":\"landing\",\"page\":\"/\"}"u8.ToArray());
        await app.Build()(context);
        Assert.Equal(202, context.Response.StatusCode);
        Assert.Equal(0, provider.GetRequiredService<TrafficRecorder>().Buffered);
    }

    [Fact]
    public async Task Report_redacts_legacy_visitors_without_losing_counts_sources_or_request_correlation()
    {
        await using var provider = Services();
        var delta = TrafficHour.Create(DateTimeOffset.UtcNow);
        delta.Record(new(DateTimeOffset.UtcNow, "request", "/", Referrer: "https://source.example", Visitor: "old-pseudonym", RequestId: "correlated-request"));
        await provider.GetRequiredService<ITrafficStore>().MergeAsync(delta, "legacy", 1, default);
        var app = new ApplicationBuilder(provider); app.UseTrafficAnalytics();
        var context = Context(provider); context.Request.Path = "/api/analytics/report";
        context.Request.Headers["X-Analytics-Key"] = new string('k', 32);
        await app.Build()(context);
        Assert.Equal(200, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var report = await JsonDocument.ParseAsync(context.Response.Body);
        var hour = report.RootElement.GetProperty("hours")[0];
        Assert.Equal(1, hour.GetProperty("counts").GetProperty("request").GetInt32());
        var item = hour.GetProperty("events")[0];
        Assert.Equal("", item.GetProperty("visitor").GetString());
        Assert.Equal("correlated-request", item.GetProperty("requestId").GetString());
        Assert.Equal("https://source.example", item.GetProperty("referrer").GetString());
        Assert.DoesNotContain("old-pseudonym", report.RootElement.GetRawText());
    }

    private static DefaultHttpContext Context(IServiceProvider? provider = null)
    {
        var context = new DefaultHttpContext { TraceIdentifier = "correlated-request" };
        if (provider is not null) context.RequestServices = provider;
        context.Request.Scheme = "https"; context.Request.Host = new("app.example");
        context.Request.Method = "GET"; context.Request.Path = "/recipes";
        context.Response.Body = new MemoryStream();
        return context;
    }
    private static ServiceProvider Services()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Analytics:AdminKey"] = new string('k', 32) }).Build();
        var services = new ServiceCollection(); services.AddLogging(); services.AddOptions();
        services.AddSingleton<IConfiguration>(config); services.AddTrafficAnalytics(config);
        return services.BuildServiceProvider();
    }
    private sealed class ForbiddenConnection : IHttpConnectionFeature
    {
        public IPAddress? RemoteIpAddress { get => throw new InvalidOperationException("Remote IP must never be read for analytics"); set => throw new NotSupportedException(); }
        public IPAddress? LocalIpAddress { get; set; }
        public int RemotePort { get; set; }
        public int LocalPort { get; set; }
        public string ConnectionId { get; set; } = "";
    }
}
