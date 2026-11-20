using System.Security.Cryptography;
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
        foreach (var path in new[] { "/", "/health/live", "/health/ready", "/api/system/readiness", "/starting", "/failed", "/starting/", "/failed/", "/startup.js", "/app.css", "/privacy", "/privacy/", "/privacy.html", "/analytics-privacy.html", "/privacy-settings.js", "/privacy.css" })
        {
            using var response = await client.GetAsync(path).WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
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
        foreach (var path in new[] { "/", "/health", "/api/system/readiness", "/robots.txt", "/wp-admin", "/Account/Login", "/demo" })
        {
            using var head = await client.SendAsync(new HttpRequestMessage(HttpMethod.Head, path));
            using var get = await client.GetAsync(path);
        }
        Assert.Equal(0, factory.Probe.Calls);
        Assert.Equal(0, factory.Initializer.Calls);
        using var scan = await client.GetAsync("/api/v1/recipes");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, scan.StatusCode);
        using var rejected = await client.PostAsync("/api/system/start", null);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Equal(0, factory.Initializer.Calls);
        using (var cookieAuditClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false }))
        {
            using var cookieResponse = await cookieAuditClient.GetAsync("/api/system/start-token");
            var antiforgeryCookie = cookieResponse.Headers.GetValues("Set-Cookie").Single(v => v.StartsWith(".AspNetCore.Antiforgery."));
            Assert.Contains("secure", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("httponly", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);
            var authCookie = factory.Services.GetRequiredService<IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>()
                .Get(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);
            Assert.Equal(TimeSpan.FromDays(14), authCookie.ExpireTimeSpan);
        }
        using var tokenResponse = await client.GetAsync("/api/system/start-token");
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        var token = System.Text.Json.JsonDocument.Parse(tokenBody).RootElement.GetProperty("token").GetString();
        var starts = await Task.WhenAll(Enumerable.Range(0, 5).Select(async _ =>
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/system/start");
            request.Headers.Add("RequestVerificationToken", token);
            return await client.SendAsync(request);
        }));
        Assert.All(starts, response => { Assert.Equal(HttpStatusCode.Accepted, response.StatusCode); response.Dispose(); });
        await WaitUntilAsync(() => factory.Initializer.Calls == 1);
        factory.Initializer.Complete();
        var readiness = factory.Services.GetRequiredService<IPersistenceReadiness>();
        await WaitUntilAsync(() => readiness.IsReady);
        Assert.Contains("\"state\":\"Ready\"", await client.GetStringAsync("/api/system/readiness"));
        Assert.Equal(0, factory.Probe.Calls);
        Assert.Single(factory.Observer.Events, e => e.Stage == "Initializing");
        Assert.All(factory.Observer.Events, e => { Assert.Equal("ExplicitApplicationStart", e.Reason); Assert.NotEmpty(e.CorrelationId); });
        Assert.Equal(new[] { "Requested", "Initializing", "Ready" }, factory.Observer.Events.Select(e => e.Stage));
        using var upload = await client.GetAsync("/uploads/recipe-images/95eb3480-fdb9-4928-bedd-f6f84e4b31cc.webp");
        Assert.Equal(HttpStatusCode.Redirect, upload.StatusCode);
        Assert.Contains("/Account/Login", upload.Headers.Location!.ToString());
    }

    [Fact]
    public async Task PassiveRequests_WithOldIdentityAndDemoCookies_NeverValidateIdentity()
    {
        using var factory = new StartupFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false, HandleCookies = false });
        var options = factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationOptions>>().Get(Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);
        var identity = new System.Security.Claims.ClaimsIdentity(new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, "old-demo-user") }, Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);
        var ticket = new Microsoft.AspNetCore.Authentication.AuthenticationTicket(new System.Security.Claims.ClaimsPrincipal(identity), new Microsoft.AspNetCore.Authentication.AuthenticationProperties { IssuedUtc = DateTimeOffset.UtcNow.AddDays(-1) }, Microsoft.AspNetCore.Identity.IdentityConstants.ApplicationScheme);
        var cookie = options.TicketDataFormat.Protect(ticket);
        foreach (var path in new[] { "/", "/starting", "/health", "/api/system/readiness", "/startup.js", "/wp-admin", "/analytics", "/analytics.html", "/analytics/dashboard.js", "/analytics-privacy.html", "/api/analytics/report" })
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, path);
            request.Headers.Add("Cookie", $"{options.Cookie.Name}={cookie}; RecipeLibrary.Demo=1");
            using var response = await client.SendAsync(request);
            Assert.True((int)response.StatusCode < 500);
        }
        Assert.Equal(0, factory.Stamp.Calls);
        Assert.Equal(0, factory.Initializer.Calls);
        Assert.Equal(0, factory.Probe.Calls);
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

    [Theory]
    [InlineData("https://test-welcome.azurestaticapps.net", true, true)]
    [InlineData("https://test-welcome.azurestaticapps.net", false, false)]
    [InlineData("https://untrusted.example", true, false)]
    public async Task Start_RequiresTrustedOriginAndNonSimpleHeader(string origin, bool header, bool accepted)
    {
        using var factory = new StartupFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/system/start");
        request.Headers.Add("Origin", origin);
        if (header) request.Headers.Add("X-Application-Start", "user");
        using var response = await client.SendAsync(request);
        Assert.Equal(accepted ? HttpStatusCode.Accepted : HttpStatusCode.Forbidden, response.StatusCode);
        if (accepted) await WaitUntilAsync(() => factory.Initializer.Calls == 1);
        else Assert.Equal(0, factory.Initializer.Calls);
        Assert.Equal(0, factory.Probe.Calls);
    }

    [Fact]
    public async Task Start_RateLimitsBeforeAdditionalDatabaseWork()
    {
        using var factory = new StartupFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        for (var i = 0; i < 91; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/system/start");
            request.Headers.Add("Origin", "https://test-welcome.azurestaticapps.net");
            request.Headers.Add("X-Application-Start", "user");
            using var response = await client.SendAsync(request);
            Assert.Equal(i < 90 ? HttpStatusCode.Accepted : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
        await WaitUntilAsync(() => factory.Initializer.Calls == 1);
        Assert.Equal(0, factory.Probe.Calls);
    }

    [Fact]
    public async Task Probe_IsSharedCachedAndRecoversAfterLaterOutage()
    {
        var startup = new PersistenceReadiness();
        var probe = new Probe();
        var clock = new Clock();
        var service = new DatabaseAvailability(startup, probe, clock, NullLogger<DatabaseAvailability>.Instance);
        Assert.Equal(PersistenceWarmupState.Starting, await service.ProbeForStartAsync(default));
        Assert.Equal(0, probe.Calls);
        startup.MarkReady();
        Assert.Equal(PersistenceWarmupState.Ready, await service.GetStateAsync(default));
        Assert.Equal(0, probe.Calls);
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => service.ProbeForStartAsync(default)));
        Assert.All(results, state => Assert.Equal(PersistenceWarmupState.Ready, state));
        Assert.Equal(1, probe.Calls);
        clock.Advance();
        probe.Fail = true;
        Assert.Equal(PersistenceWarmupState.Starting, await service.ProbeForStartAsync(default));
        Assert.True(startup.IsReady);
        clock.Advance();
        probe.Fail = false;
        Assert.Equal(PersistenceWarmupState.Ready, await service.ProbeForStartAsync(default));
        Assert.Equal(3, probe.Calls);
        Assert.Equal(1, probe.MaxConcurrent);
    }

    [Fact]
    public async Task AutomaticWarmup_InitializesWithoutVisitor_AndKeepsSingleFlight()
    {
        var initializer = new BlockedInitializer();
        var readiness = new PersistenceReadiness();
        var observer = new Observer();
        var wake = new ApplicationWakeRequest([observer], NullLogger<ApplicationWakeRequest>.Instance);
        using var service = new PersistenceWarmupHostedService(initializer,
            Options.Create(new PersistenceWarmupOptions { AutoStart = true }), readiness,
            NullLogger<PersistenceWarmupHostedService>.Instance, wake);
        await service.StartAsync(default);
        await WaitUntilAsync(() => initializer.Calls == 1);
        Assert.False(readiness.IsReady);
        wake.Request("ExplicitApplicationStart", "later-visitor");
        initializer.Complete();
        await WaitUntilAsync(() => readiness.IsReady);
        Assert.Equal(1, initializer.Calls);
        Assert.All(observer.Events, entry =>
        {
            Assert.Equal("AutomaticStartup", entry.Reason);
            Assert.Equal("startup", entry.CorrelationId);
        });
        Assert.Equal(new[] { "Requested", "Initializing", "Ready" }, observer.Events.Select(e => e.Stage));
        await service.StopAsync(default);
    }

    [Fact]
    public async Task InitializerFailure_IsReportedWithoutCrashingHost()
    {
        var startup = new PersistenceReadiness();
        using var service = new PersistenceWarmupHostedService(new BrokenInitializer(),
            Options.Create(new PersistenceWarmupOptions()), startup, NullLogger<PersistenceWarmupHostedService>.Instance, RequestedWake());
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
            startup, NullLogger<PersistenceWarmupHostedService>.Instance, RequestedWake());
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

    private static ApplicationWakeRequest RequestedWake()
    {
        var wake = new ApplicationWakeRequest([], NullLogger<ApplicationWakeRequest>.Instance);
        wake.Request("TestExplicitStart", "test-correlation");
        return wake;
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
        public Observer Observer { get; } = new();
        public StampValidator Stamp { get; } = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            using var signingKey = RSA.Create(2048);
            using var encryptionKey = RSA.Create(2048);
            builder.UseSetting("OpenIddict:SigningKey", signingKey.ExportPkcs8PrivateKeyPem());
            builder.UseSetting("OpenIddict:EncryptionKey", encryptionKey.ExportPkcs8PrivateKeyPem());
            builder.UseSetting("ConnectionStrings:RecipeDb", "Server=localhost;Database=unused;Integrated Security=True;TrustServerCertificate=True");
            builder.UseSetting("Welcome:AllowedOrigins:0", "https://recipelibrary.vasoftware.nl");
            builder.UseSetting("Welcome:AllowedOrigins:1", "https://test-welcome.azurestaticapps.net");
            builder.UseSetting("ContentModeration:AdminEmails:0", "admin@example.invalid");
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IApplicationWakeObserver>(Observer);
                services.RemoveAll<Microsoft.AspNetCore.Identity.ISecurityStampValidator>();
                services.AddSingleton<Microsoft.AspNetCore.Identity.ISecurityStampValidator>(Stamp);
                services.RemoveAll<IPersistenceInitializer>();
                services.AddSingleton<IPersistenceInitializer>(Initializer);
                services.RemoveAll<IDatabaseProbe>();
                services.AddSingleton<IDatabaseProbe>(Probe);
            });
        }
    }

    private sealed class StampValidator : Microsoft.AspNetCore.Identity.ISecurityStampValidator
    {
        public int Calls;
        public Task ValidateAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieValidatePrincipalContext context)
        {
            Interlocked.Increment(ref Calls);
            throw new InvalidOperationException("Passive requests must not validate SQL-backed cookies.");
        }
    }

    private sealed class Observer : IApplicationWakeObserver
    {
        public System.Collections.Concurrent.ConcurrentQueue<ApplicationWakeEvent> Events { get; } = new();
        public void Observe(ApplicationWakeEvent wakeEvent) => Events.Enqueue(wakeEvent);
    }

    private sealed class BlockedInitializer : IPersistenceInitializer
    {
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public bool CancellationObserved { get; private set; }
        public void Complete() => _completion.TrySetResult();
        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
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
