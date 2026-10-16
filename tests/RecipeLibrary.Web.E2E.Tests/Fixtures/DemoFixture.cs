using Microsoft.Playwright;
using RecipeLibrary.Testing;
using Xunit;

namespace RecipeLibrary.Web.E2E.Tests.Fixtures;

public sealed class DemoFixture : IAsyncLifetime
{
    private readonly TestSqlDatabase database = new();
    private readonly E2eProcessHost host = new();
    private IPlaywright? playwright;
    public IBrowser Browser { get; private set; } = null!;
    public string Diagnostics => host.Diagnostics;
    public string BaseUrl => host.BaseUrl;
    public string ConnectionString => database.ConnectionString;

    public async Task InitializeAsync()
    {
        await database.StartAsync();
        await host.StartAsync(database.ConnectionString, settings: new Dictionary<string, string>
        {
            ["Demo__Enabled"] = "true", ["Demo__StartsPerIpPerHour"] = "1000",
            ["ContentModeration__Enabled"] = "false", ["Demo__LifetimeMinutes"] = "1"
        });
        playwright = await Playwright.CreateAsync();
        Browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null) await Browser.DisposeAsync();
        playwright?.Dispose();
        await host.DisposeAsync();
        await database.DisposeAsync();
    }
}

[CollectionDefinition(nameof(DemoE2eCollection))]
public sealed class DemoE2eCollection : ICollectionFixture<DemoFixture>;
