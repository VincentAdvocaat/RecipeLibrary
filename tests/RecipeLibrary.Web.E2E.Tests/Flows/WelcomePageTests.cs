using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

/// <summary>Real browser tests for the static frontend; no SQL or Azure resources required.</summary>
public sealed class WelcomePageTests : IAsyncLifetime
{
    private WebApplication _host = null!;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private string _url = "";

    public async Task InitializeAsync()
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../.."));
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        _host = builder.Build();
        _host.MapGet("/", async () => Microsoft.AspNetCore.Http.Results.Content(
            (await File.ReadAllTextAsync(Path.Combine(repo, "src/Welcome/index.html")))
                .Replace("__BACKEND_URL__", "https://backend.example"), "text/html"));
        _host.MapGet("/style.css", () => Microsoft.AspNetCore.Http.Results.File(Path.Combine(repo, "src/Welcome/style.css"), "text/css"));
        _host.MapGet("/startup.js", () => Microsoft.AspNetCore.Http.Results.File(
            Path.Combine(repo, "src/Web/RecipeLibrary.Web/wwwroot/startup.js"), "text/javascript"));
        await _host.StartAsync();
        _url = _host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    [Fact]
    public async Task OfflineBackend_ShowsWelcomeAndLanguageChoice()
    {
        var page = await _browser.NewPageAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        await page.RouteAsync("https://backend.example/**", route => route.AbortAsync());
        await page.GotoAsync(_url);
        await Expect(page.GetByRole(AriaRole.Heading)).ToHaveTextAsync("Welkom bij RecipeLibrary");
        await Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("automatisch");
        await page.GetByRole(AriaRole.Button, new() { Name = "English", Exact = true }).ClickAsync();
        await Expect(page.GetByRole(AriaRole.Heading)).ToHaveTextAsync("Welcome to RecipeLibrary");
        var screenshot = Path.Combine(Path.GetTempPath(), "RecipeLibrary-welcome-mobile.png");
        await page.ScreenshotAsync(new() { Path = screenshot, FullPage = true });
        await page.CloseAsync();
    }

    [Theory]
    [InlineData("starting")]
    [InlineData("invalid-json")]
    [InlineData("http-error")]
    public async Task TemporaryFailure_ThenReady_NavigatesToOriginalRecipe(string initial)
    {
        var page = await _browser.NewPageAsync();
        var calls = 0;
        await page.RouteAsync("https://backend.example/**", async route =>
        {
            if (!route.Request.Url.Contains("/api/system/readiness"))
            {
                await route.FulfillAsync(new() { ContentType = "text/html", Body = "<h1>Recipe destination</h1>" });
                return;
            }
            var first = Interlocked.Increment(ref calls) == 1;
            await route.FulfillAsync(new()
            {
                Status = first && initial == "http-error" ? 503 : 200,
                ContentType = "application/json",
                Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
                Body = first ? initial == "invalid-json" ? "not JSON" : "{\"state\":\"Starting\"}" : "{\"state\":\"Ready\"}"
            });
        });
        await page.GotoAsync(_url + "/?returnUrl=%2Frecipes%2F42%3Ftab%3Dingredients");
        await page.WaitForURLAsync("https://backend.example/recipes/42?tab=ingredients", new() { Timeout = 15000 });
        Assert.Equal(2, calls);
        await page.CloseAsync();
    }

    [Fact]
    public async Task PermanentFailure_StopsAndRetryRecovers_RejectingExternalReturnUrl()
    {
        var page = await _browser.NewPageAsync();
        var calls = 0;
        await page.RouteAsync("https://backend.example/**", route => route.FulfillAsync(new()
        {
            ContentType = "application/json",
            Headers = new Dictionary<string, string> { ["Access-Control-Allow-Origin"] = "*" },
            Body = route.Request.Url.Contains("/api/system/readiness")
                ? Interlocked.Increment(ref calls) == 1 ? "{\"state\":\"Failed\"}" : "{\"state\":\"Ready\"}"
                : "{}"
        }));
        await page.GotoAsync(_url + "/?returnUrl=https%3A%2F%2Fevil.example");
        await Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync("kon niet worden gestart");
        Assert.Equal(1, calls);
        await page.GetByRole(AriaRole.Button, new() { Name = "Opnieuw proberen" }).ClickAsync();
        await page.WaitForURLAsync("https://backend.example/");
        Assert.Equal(2, calls);
        await page.CloseAsync();
    }

    [Fact]
    public async Task WithoutJavascript_LinkStillOpensApplication()
    {
        var page = await _browser.NewPageAsync(new() { JavaScriptEnabled = false });
        await page.GotoAsync(_url);
        await Expect(page.GetByRole(AriaRole.Link, new() { Name = "Naar de applicatie" }))
            .ToHaveAttributeAsync("href", "https://backend.example/");
        await page.CloseAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null) await _browser.DisposeAsync();
        _playwright?.Dispose();
        if (_host is not null) await _host.DisposeAsync();
    }
}
