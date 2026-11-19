using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Web.Analytics;
using RecipeLibrary.Web.Middleware;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

public sealed class AnalyticsStatusPagesTests
{
    [Theory]
    [InlineData("unauthorized-report", 401)]
    [InlineData("forbidden-visit", 403)]
    [InlineData("forbidden-preflight", 403)]
    [InlineData("oversized-visit", 413)]
    [InlineData("limited-report", 429)]
    [InlineData("unsupported-method", 405)]
    [InlineData("valid-report", 200)]
    [InlineData("valid-visit", 202)]
    public async Task Analytics_responses_bypass_status_page_reexecution_and_unready_persistence(string scenario, int expected)
    {
        await using var pipeline = new Pipeline();
        var context = pipeline.Context(scenario.Contains("visit") || scenario.Contains("preflight") ? "/api/analytics/visit" : "/api/analytics/report");
        context.Request.Method = scenario.Contains("visit") ? "POST" : scenario.Contains("preflight") ? "OPTIONS" : "GET";
        if (scenario == "unsupported-method") context.Request.Method = "DELETE";
        if (scenario != "unauthorized-report") context.Request.Headers["X-Analytics-Key"] = Pipeline.Key;
        context.Request.Headers.Origin = scenario.StartsWith("forbidden") ? "https://untrusted.example" : "https://app.example";
        context.Request.ContentType = "application/json";
        context.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes("{\"event\":\"landing\",\"page\":\"/\"}"));
        if (scenario == "oversized-visit") context.Request.ContentLength = 2049;
        using var exhausted = scenario == "limited-report" ? pipeline.Services.GetRequiredService<TrafficEndpointLimits>().Reports.AttemptAcquire(6) : null;

        await pipeline.Invoke(context);

        Assert.Equal(expected, context.Response.StatusCode);
        Assert.Empty(context.Response.Headers.Location.ToString());
        Assert.Empty(pipeline.DownstreamPaths);
        Assert.False(pipeline.Services.GetRequiredService<IPersistenceReadiness>().IsReady);
        if (scenario == "valid-report")
        {
            context.Response.Body.Position = 0;
            using var report = await JsonDocument.ParseAsync(context.Response.Body);
            Assert.True(report.RootElement.TryGetProperty("hours", out _));
        }
        if (scenario == "valid-visit") Assert.Equal(1, pipeline.Services.GetRequiredService<TrafficRecorder>().Buffered);
    }

    [Fact]
    public async Task Ordinary_page_errors_still_reexecute_through_readiness()
    {
        await using var pipeline = new Pipeline();
        // Health paths pass readiness, allowing the terminal 404 to trigger the configured status page.
        var context = pipeline.Context("/health/missing");
        await pipeline.Invoke(context);
        Assert.Equal(302, context.Response.StatusCode);
        Assert.StartsWith("/starting?", context.Response.Headers.Location.ToString());
        Assert.Equal(new[] { "/health/missing", "/not-found" }, pipeline.DownstreamPaths);
    }

    private sealed class Pipeline : IAsyncDisposable
    {
        public const string Key = "12345678901234567890123456789012";
        public ServiceProvider Services { get; }
        public List<string> DownstreamPaths { get; } = [];
        public RequestDelegate Invoke { get; }
        public Pipeline()
        {
            var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Analytics:AdminKey"] = Key }).Build();
            var services = new ServiceCollection();
            services.AddLogging(); services.AddOptions();
            services.AddSingleton<IConfiguration>(config);
            services.AddSingleton<IPersistenceReadiness, PersistenceReadiness>();
            services.AddTrafficAnalytics(config);
            Services = services.BuildServiceProvider();
            var app = new ApplicationBuilder(Services);
            app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
            app.UseTrafficAnalytics();
            app.Use(async (context, next) => { DownstreamPaths.Add(context.Request.Path.Value!); await next(context); });
            app.UseMiddleware<PersistenceReadinessMiddleware>();
            app.Run(context => { context.Response.StatusCode = 404; return Task.CompletedTask; });
            Invoke = app.Build();
        }
        public DefaultHttpContext Context(string path)
        {
            var context = new DefaultHttpContext { RequestServices = Services };
            context.Request.Path = path;
            context.Request.Method = "GET";
            context.Request.Scheme = "https";
            context.Request.Host = new("app.example");
            context.Response.Body = new MemoryStream();
            return context;
        }
        public ValueTask DisposeAsync() => Services.DisposeAsync();
    }
}
