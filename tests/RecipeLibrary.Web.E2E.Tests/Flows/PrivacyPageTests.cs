using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Xunit;
using static Microsoft.Playwright.Assertions;

public sealed class PrivacyPageTests : IAsyncLifetime
{
    private WebApplication host=null!;
    private IPlaywright pw=null!;
    private IBrowser browser=null!;
    private string url="";
    private int visits;
    public async Task InitializeAsync()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../src/Web/RecipeLibrary.Web/wwwroot"));
        var builder=WebApplication.CreateBuilder(); builder.WebHost.UseUrls("http://127.0.0.1:0");
        host=builder.Build();
        host.MapGet("/",()=>Microsoft.AspNetCore.Http.Results.Content("<html><body><h1>Test</h1><script type='module' src='/browserTracker.js'></script></body></html>","text/html"));
        foreach(var file in new[]{"privacy.html","privacy.css","privacy-settings.js","browserTracker.js"})
            host.MapGet("/"+file,()=>Microsoft.AspNetCore.Http.Results.File(Path.Combine(root,file),file.EndsWith(".js")?"text/javascript":file.EndsWith(".css")?"text/css":"text/html"));
        host.MapPost("/api/analytics/visit",()=>{Interlocked.Increment(ref visits);return Microsoft.AspNetCore.Http.Results.Accepted();});
        await host.StartAsync(); url=host.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        pw=await Playwright.CreateAsync(); browser=await pw.Chromium.LaunchAsync(new(){Headless=true});
    }
    [Theory]
    [InlineData(1280,900)] [InlineData(390,844)]
    public async Task Preference_is_functional_host_only_and_stops_future_beacons(int width,int height)
    {
        await using var context=await browser.NewContextAsync(new(){ViewportSize=new(){Width=width,Height=height}});
        var page=await context.NewPageAsync();
        await page.GotoAsync(url);
        await page.WaitForFunctionAsync("() => performance.getEntriesByType('resource').some(r=>r.name.includes('/api/analytics/visit'))");
        Assert.Equal(1,Volatile.Read(ref visits));
        await page.GotoAsync(url+"/privacy.html");
        await Expect(page.GetByRole(AriaRole.Heading,new(){Name="Privacy en cookies",Exact=true})).ToBeVisibleAsync();
        await page.Locator("[data-analytics-choice=off]").ClickAsync();
        await Expect(page.Locator("#privacy-choice-status")).ToContainTextAsync("staan uit");
        var cookie=Assert.Single(await context.CookiesAsync(), c=>c.Name=="RecipeLibrary.Analytics");
        Assert.Equal("off",cookie.Value); Assert.Equal("127.0.0.1",cookie.Domain); Assert.Equal("/",cookie.Path);
        Assert.Equal(SameSiteAttribute.Lax,cookie.SameSite); Assert.False(cookie.HttpOnly);
        Assert.True(await page.EvaluateAsync<bool>("document.documentElement.scrollWidth <= innerWidth"));
        await page.GotoAsync(url);
        await page.EvaluateAsync("import('/browserTracker.js').then(m => m.recordVisit('browser'))");
        Assert.Equal(1,Volatile.Read(ref visits));
        await page.GotoAsync(url+"/privacy.html");
        await page.Locator("[data-analytics-choice=default]").ClickAsync();
        Assert.Empty((await context.CookiesAsync()).Where(c=>c.Name=="RecipeLibrary.Analytics"));
        await page.GotoAsync(url);
        await page.WaitForFunctionAsync("() => performance.getEntriesByType('resource').some(r=>r.name.includes('/api/analytics/visit'))");
        Assert.Equal(2,Volatile.Read(ref visits));
    }
    [Fact]
    public async Task Browser_GPC_remains_effective_after_clearing_the_cookie()
    {
        await using var context=await browser.NewContextAsync();
        await context.AddInitScriptAsync("Object.defineProperty(navigator,'globalPrivacyControl',{get:()=>true})");
        var page=await context.NewPageAsync();
        await page.GotoAsync(url+"/privacy.html");
        await page.Locator("[data-analytics-choice=off]").ClickAsync();
        await page.Locator("[data-analytics-choice=default]").ClickAsync();
        await Expect(page.Locator("#privacy-choice-status")).ToContainTextAsync("DNT/GPC");
        await page.GotoAsync(url);
        await page.EvaluateAsync("import('/browserTracker.js').then(m => m.recordVisit('browser'))");
        Assert.Equal(0,Volatile.Read(ref visits));
    }
    public async Task DisposeAsync(){if(browser is not null)await browser.DisposeAsync();pw?.Dispose();if(host is not null)await host.DisposeAsync();}
}
