using Microsoft.Playwright;
using RecipeLibrary.Testing;
using Testcontainers.MsSql;
using Xunit;

namespace RecipeLibrary.Web.E2E.Tests.Fixtures;

public sealed class E2eFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private E2eProcessHost? _host;
    private IPlaywright? _playwright;

    public TestSeedData Seed { get; private set; } = null!;
    public string BaseUrl { get; private set; } = string.Empty;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = _container.GetConnectionString();
        Environment.SetEnvironmentVariable("ConnectionStrings__RecipeDb", connectionString);

        Seed = await TestDataSeeder.SeedWithConnectionStringAsync(connectionString);

        _host = new E2eProcessHost();
        await _host.StartAsync(connectionString);
        BaseUrl = _host.BaseUrl;

        _playwright = await Playwright.CreateAsync();
        var headless = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_HEADED"));
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless });
    }

    public async Task DisposeAsync()
    {
        if (Browser is not null)
        {
            await Browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (_host is not null)
        {
            await _host.DisposeAsync();
        }

        await _container.DisposeAsync();
    }
}

[CollectionDefinition(nameof(RecipeOverviewE2eCollection))]
public sealed class RecipeOverviewE2eCollection : ICollectionFixture<E2eFixture>;

[CollectionDefinition(nameof(RecipeCrudE2eCollection))]
public sealed class RecipeCrudE2eCollection : ICollectionFixture<E2eFixture>;

[CollectionDefinition(nameof(ShoppingListE2eCollection))]
public sealed class ShoppingListE2eCollection : ICollectionFixture<E2eFixture>;

[CollectionDefinition(nameof(LocalizationE2eCollection))]
public sealed class LocalizationE2eCollection : ICollectionFixture<E2eFixture>;
