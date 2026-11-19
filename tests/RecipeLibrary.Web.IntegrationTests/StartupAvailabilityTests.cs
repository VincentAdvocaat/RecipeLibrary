using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Web.Services;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

public sealed class StartupAvailabilityTests
{
    [Fact]
    public async Task BlockedInitialization_DoesNotBlockHttp_AndStartupHasNoCircuit()
    {
        using var factory = new StartupFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });
        foreach (var path in new[] { "/health/live", "/health/ready", "/api/system/readiness", "/starting", "/failed", "/starting/", "/failed/", "/startup.js", "/app.css" })
        {
            using var response = await client.GetAsync(path).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadAsStringAsync();
            if (path == "/api/system/readiness")
            {
                Assert.Contains("\"state\":\"Starting\"", body);
                Assert.True(response.Headers.CacheControl!.NoStore);
            }
            if (path.TrimEnd('/') is "/starting" or "/failed")
            {
                Assert.Contains("startup.js", body);
                Assert.DoesNotContain("\"type\":\"server\"", body);
            }
        }
        Assert.Equal(0, factory.Probe.Calls);
        factory.Initializer.Complete();
        var readiness = factory.Services.GetRequiredService<IPersistenceReadiness>();
        await WaitUntilAsync(() => readiness.IsReady);
        Assert.Contains("\"state\":\"Ready\"", await client.GetStringAsync("/api/system/readiness"));
        Assert.Equal(1, factory.Probe.Calls);
        using var upload = await client.GetAsync("/uploads/recipe-images/95eb3480-fdb9-4928-bedd-f6f84e4b31cc.webp");
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        Assert.Contains("/Account/Login", upload.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("https://recipelibrary.vasoftware.nl", true)]
    [InlineData("https://test-welcome.azurestaticapps.net", true)]
    [InlineData("https://untrusted.example", false)]
    public async Task Cors_OnlyAllowsConfiguredOriginsOnReadiness(string origin, bool allowed)
    {
        using var factory = new StartupFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/system/readiness");
        request.Headers.Add("Origin", origin);
        using var response = await client.SendAsync(request);
        Assert.Equal(allowed, response.Headers.Contains("Access-Control-Allow-Origin"));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
        using var health = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        health.Headers.Add("Origin", origin);
        using var healthResponse = await client.SendAsync(health);
        Assert.False(healthResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task Probe_IsSharedCachedAndRecoversAfterLaterOutage()
    {
        var startup = new PersistenceReadiness();
        var probe = new Probe();
        var clock = new Clock();
        var service = new DatabaseAvailability(startup, probe, clock, NullLogger<DatabaseAvailability>.Instance);
        Assert.Equal(PersistenceWarmupState.Starting, await service.GetStateAsync(default));
        Assert.Equal(0, probe.Calls);
        startup.MarkReady();
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => service.GetStateAsync(default)));
        Assert.All(results, state => Assert.Equal(PersistenceWarmupState.Ready, state));
        Assert.Equal(1, probe.Calls);
        clock.Advance();
        probe.Fail = true;
        Assert.Equal(PersistenceWarmupState.Starting, await service.GetStateAsync(default));
        Assert.True(startup.IsReady);
        clock.Advance();
        probe.Fail = false;
        Assert.Equal(PersistenceWarmupState.Ready, await service.GetStateAsync(default));
        Assert.Equal(3, probe.Calls);
        Assert.Equal(1, probe.MaxConcurrent);
    }

    [Fact]
    public async Task InitializerFailure_IsReportedWithoutCrashingHost()
    {
        var startup = new PersistenceReadiness();
        using var service = new PersistenceWarmupHostedService(new BrokenInitializer(),
            Options.Create(new PersistenceWarmupOptions()), startup, NullLogger<PersistenceWarmupHostedService>.Instance);
        await service.StartAsync(default);
        await WaitUntilAsync(() => startup.HasPermanentlyFailed);
        await service.StopAsync(default);
    }

    [Fact]
    public async Task WarmupDeadline_CancelsInFlightInitialization()
    {
        var startup = new PersistenceReadiness();
        var initializer = new BlockedInitializer();
        using var service = new PersistenceWarmupHostedService(initializer,
            Options.Create(new PersistenceWarmupOptions { Timeout = TimeSpan.FromMilliseconds(50) }),
            startup, NullLogger<PersistenceWarmupHostedService>.Instance);
        await service.StartAsync(default);
        await WaitUntilAsync(() => startup.HasPermanentlyFailed);
        Assert.True(initializer.CancellationObserved);
        await service.StopAsync(default);
    }

    [Theory]
    [InlineData("/recipes/42?tab=ingredients", "/recipes/42?tab=ingredients")]
    [InlineData("https://evil.example", "/")]
    [InlineData("//evil.example", "/")]
    [InlineData("/%2fevil.example", "/")]
    [InlineData("/\\evil.example", "/")]
    [InlineData("/starting?returnUrl=/starting", "/")]
    [InlineData("/failed/", "/")]
    [InlineData("/a/../starting", "/")]
    public void ReturnUrl_OnlyAllowsLocalNonStartupPaths(string value, string expected) =>
        Assert.Equal(expected, StartupReturnUrl.Normalize(value));

    [Fact]
    public async Task StartupRedirect_PreservesQuery_AndNeverRedirectsPostedForms()
    {
        var middleware = new RecipeLibrary.Web.Middleware.PersistenceReadinessMiddleware(_ => Task.CompletedTask);
        var request = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        request.Request.Method = "GET";
        request.Request.Path = "/recipes/42";
        request.Request.QueryString = new Microsoft.AspNetCore.Http.QueryString("?tab=ingredients");
        await middleware.InvokeAsync(request, new PersistenceReadiness());
        Assert.Equal("/starting?returnUrl=%2Frecipes%2F42%3Ftab%3Dingredients", request.Response.Headers.Location);

        var post = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        post.Request.Method = "POST";
        post.Request.Path = "/Account/Login";
        post.Response.Body = new MemoryStream();
        await middleware.InvokeAsync(post, new PersistenceReadiness());
        Assert.Equal(503, post.Response.StatusCode);
        Assert.Empty(post.Response.Headers.Location.ToString());
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition()) await Task.Delay(10, deadline.Token);
    }

    private sealed class StartupFactory : WebApplicationFactory<Program>
    {
        public BlockedInitializer Initializer { get; } = new();
        public Probe Probe { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:RecipeDb", "Server=localhost;Database=unused;Integrated Security=True;TrustServerCertificate=True");
            builder.UseSetting("Welcome:AllowedOrigins:0", "https://recipelibrary.vasoftware.nl");
            builder.UseSetting("Welcome:AllowedOrigins:1", "https://test-welcome.azurestaticapps.net");
            builder.UseSetting("ContentModeration:AdminEmails:0", "admin@example.invalid");
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IPersistenceInitializer>();
                services.AddSingleton<IPersistenceInitializer>(Initializer);
                services.RemoveAll<IDatabaseProbe>();
                services.AddSingleton<IDatabaseProbe>(Probe);
            });
        }
    }

    private sealed class BlockedInitializer : IPersistenceInitializer
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool CancellationObserved { get; private set; }
        public void Complete() => _completion.TrySetResult();
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            try { await _completion.Task.WaitAsync(cancellationToken); }
            catch (OperationCanceledException) { CancellationObserved = true; throw; }
        }
    }

    private sealed class BrokenInitializer : IPersistenceInitializer
    {
        public Task InitializeAsync(CancellationToken cancellationToken) => throw new InvalidOperationException("Invalid schema");
    }

    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance() => _now += TimeSpan.FromSeconds(6);
    }

    private sealed class Probe : IDatabaseProbe
    {
        public int Calls;
        private int _concurrent;
        public int MaxConcurrent;
        public bool Fail;
        public async Task CheckAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            MaxConcurrent = Math.Max(MaxConcurrent, Interlocked.Increment(ref _concurrent));
            try
            {
                await Task.Delay(20, cancellationToken);
                if (Fail) throw new TimeoutException();
            }
            finally { Interlocked.Decrement(ref _concurrent); }
        }
    }
}
