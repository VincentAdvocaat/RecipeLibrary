using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

public sealed class AnalyticsDashboardTests : IAsyncLifetime
{
    private WebApplication host = null!;
    private IPlaywright playwright = null!;
    private IBrowser browser = null!;
    private string url = "";
    private int reportCalls;
    private const string Key = "analytics-test-secret-with-32-characters";

    public async Task InitializeAsync()
    {
        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/Web/RecipeLibrary.Web/wwwroot"));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        host = builder.Build();
        host.MapGet("/analytics.html", () => Microsoft.AspNetCore.Http.Results.File(Path.Combine(root, "analytics.html"), "text/html"));
        host.MapGet("/analytics/dashboard.js", () => Microsoft.AspNetCore.Http.Results.File(Path.Combine(root, "analytics/dashboard.js"), "text/javascript"));
        host.MapGet("/analytics/dashboard.css", () => Microsoft.AspNetCore.Http.Results.File(Path.Combine(root, "analytics/dashboard.css"), "text/css"));
        host.MapGet("/api/analytics/report", (Microsoft.AspNetCore.Http.HttpContext context) =>
        {
            Interlocked.Increment(ref reportCalls);
            if (context.Request.Headers["X-Analytics-Key"] != Key) return Microsoft.AspNetCore.Http.Results.Unauthorized();
            return Microsoft.AspNetCore.Http.Results.Json(new
            {
                generatedAt = DateTimeOffset.UtcNow,
                persistence = new { configured = true, available = true, lastSuccess = DateTimeOffset.UtcNow, buffered = 0, dropped = 0 },
                hours = new[] { new { hour = DateTimeOffset.UtcNow, counts = new Dictionary<string,int> { ["request"] = 42, ["database-start-requested"] = 1 },
                    paths = new Dictionary<string,int> { ["/robots.txt"] = 42 }, referrers = new Dictionary<string,int> { ["https://example.org"] = 4 }, bots = new Dictionary<string,int> { ["Googlebot-inferred"] = 3 },
                    events = new[] { new { at = DateTimeOffset.UtcNow, kind = "request", path = "/<script>alert(1)</script>", status = 404, referrer = "https://example.org", browser = "Other", bot = "unknown", visitor = "a123" } }, droppedDetails = 0 } },
                limits = new { retentionDays = 31, detailsPerHour = 200 }
            });
        });
        await host.StartAsync();
        url = host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        playwright = await Playwright.CreateAsync();
        browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    [Theory]
    [InlineData(1280, 900)]
    [InlineData(390, 844)]
    public async Task Dashboard_RequiresKey_RendersSafeData_AndSupportsLogout(int width, int height)
    {
        await using var context = await browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        var page = await context.NewPageAsync();
        var dialogs = 0;
        page.Dialog += (_, _) => dialogs++;
        await page.GotoAsync(url + "/analytics.html");
        await Expect(page.GetByRole(HeadingRole, new() { Name = "Analytics", Exact = true })).ToBeVisibleAsync();
        Assert.Equal(0, reportCalls);
        await page.Locator("#key").FillAsync(Key);
        await page.GetByRole(AriaRole.Button, new() { Name = "Dashboard openen" }).ClickAsync();
        await Expect(page.Locator("#stats")).ToContainTextAsync("42");
        await Expect(page.Locator("#referrers")).ToContainTextAsync("https://example.org");
        await Expect(page.Locator("#events")).ToContainTextAsync("/<script>alert(1)</script>");
        Assert.Equal(0, dialogs);
        await page.ScreenshotAsync(new() { Path = Path.Combine(Path.GetTempPath(), $"RecipeLibrary-analytics-{width}.png"), FullPage = true });
        await page.GetByRole(AriaRole.Button, new() { Name = "Afmelden" }).ClickAsync();
        await Expect(page.Locator("#access")).ToBeVisibleAsync();
        Assert.Null(await page.EvaluateAsync<string?>("sessionStorage.getItem('recipelibrary-analytics-key')"));
    }

    private static AriaRole HeadingRole => AriaRole.Heading;
    public async Task DisposeAsync()
    {
        if (browser is not null) await browser.DisposeAsync();
        playwright?.Dispose();
        if (host is not null) await host.DisposeAsync();
    }
}
