using Microsoft.Playwright;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RecipeLibrary.Infrastructure.Identity;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Testing;
using Testcontainers.MsSql;
using Xunit;

namespace RecipeLibrary.Web.E2E.Tests.Fixtures;

public sealed class E2eFixture : IAsyncLifetime
{
    private readonly TestSqlDatabase _container = new();

    private E2eProcessHost? _host;
    private IPlaywright? _playwright;
    private string _authenticationState = string.Empty;

    public TestSeedData Seed { get; private set; } = null!;
    public string BaseUrl { get; private set; } = string.Empty;
    public IBrowser Browser { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        var connectionString = _container.ConnectionString;
        Environment.SetEnvironmentVariable("ConnectionStrings__RecipeDb", connectionString);

        Seed = await TestDataSeeder.SeedWithConnectionStringAsync(connectionString);
        await using (var db = new RecipeDbContext(new DbContextOptionsBuilder<RecipeDbContext>().UseSqlServer(connectionString).Options))
        {
            var user = new ApplicationUser
            {
                Id = TestDataSeeder.TestOwnerUserId, UserName = "testuser", NormalizedUserName = "TESTUSER",
                Email = "e2e@example.invalid", NormalizedEmail = "E2E@EXAMPLE.INVALID", EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString()
            };
            user.PasswordHash = new PasswordHasher<ApplicationUser>().HashPassword(user, "E2eTestPass123");
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }

        _host = new E2eProcessHost();
        await _host.StartAsync(connectionString);
        BaseUrl = _host.BaseUrl;

        _playwright = await Playwright.CreateAsync();
        var headless = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_HEADED"));
        Browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = headless });
        await using var context = await Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(BaseUrl + "/Account/Login");
        await page.Locator("#login-email").FillAsync("e2e@example.invalid");
        await page.Locator("#login-password").FillAsync("E2eTestPass123");
        await page.Locator("button[type=submit]").ClickAsync();
        await Assertions.Expect(page.GetByTestId(RecipeLibrary.Web.Testing.UiTestIds.OverviewReady)).ToBeVisibleAsync();
        _authenticationState = await context.StorageStateAsync();
    }

    public Task<IBrowserContext> NewAuthenticatedContextAsync() =>
        Browser.NewContextAsync(new() { StorageState = _authenticationState });

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
