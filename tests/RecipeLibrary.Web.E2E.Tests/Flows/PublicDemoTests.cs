using Microsoft.Playwright;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

[Collection(nameof(DemoE2eCollection))]
public sealed class PublicDemoTests(DemoFixture fixture)
{
    [Theory]
    [InlineData(1280, 900)]
    [InlineData(390, 844)]
    public async Task DemoStartsWithoutLogin_AndSupportsEditingAndReset(int width, int height)
    {
        await using var context = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = width, Height = height } });
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl + "/demo");
        await Assertions.Expect(page.GetByTestId(UiTestIds.DemoBanner)).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId(UiTestIds.OverviewReady)).ToBeVisibleAsync();
        Assert.DoesNotContain("/Account/Login", page.Url);
        var screenshotPath = Path.Combine(AppContext.BaseDirectory, "TestResults");
        Directory.CreateDirectory(screenshotPath);
        await page.Locator("img[src^='/images/demo/']").EvaluateAllAsync<object>("images => Promise.all(images.map(image => { image.loading = 'eager'; return image.decode(); }))");
        await page.ScreenshotAsync(new() { Path = Path.Combine(screenshotPath, $"demo-{width}.png"), FullPage = true });
        await Assertions.Expect(page.Locator("img[src^='/images/demo/']")).ToHaveCountAsync(8);

        await page.GotoCreateRecipeAsync(fixture.BaseUrl);
        await page.GetByTestId(UiTestIds.RecipeTitle).FillBlazorInputAsync("Mijn demorecept");
        await page.GetByTestId(UiTestIds.IngredientRowName(0)).FillBlazorInputAsync("Demo proefgroente");
        await page.GetByTestId(UiTestIds.StepInput(0)).FillBlazorInputAsync("Snijd en serveer.");
        await page.SaveRecipeAndWaitForDetailAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Mijn demorecept" })).ToBeVisibleAsync();
        await page.ReloadAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Mijn demorecept" })).ToBeVisibleAsync();

        await page.GetByTestId(UiTestIds.DemoReset).ClickAsync();
        await page.GetByTestId(UiTestIds.ConfirmYes).ClickAsync();
        await Assertions.Expect(page.GetByTestId(UiTestIds.OverviewReady)).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Mijn demorecept", new() { Exact = true })).ToHaveCountAsync(0);
        await page.GotoAsync(fixture.BaseUrl + "/shopping-list");
        await Assertions.Expect(page.GetByTestId(UiTestIds.DemoBanner)).ToBeVisibleAsync();
        await page.GotoAsync(fixture.BaseUrl + "/pantry");
        await Assertions.Expect(page.GetByText("olijfolie", new() { Exact = true })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ImportsAndUploadsOfferRegistration_RegistrationDoesNotCopyDemo()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl + "/demo");
        await Assertions.Expect(page.GetByTestId(UiTestIds.DemoBanner)).ToBeVisibleAsync();
        await page.GotoAsync(fixture.BaseUrl + "/recipes/import");
        await Assertions.Expect(page.GetByTestId(UiTestIds.DemoAccountRequired)).ToBeVisibleAsync();
        await page.GotoCreateRecipeAsync(fixture.BaseUrl);
        await Assertions.Expect(page.GetByTestId(UiTestIds.DemoAccountRequired).First).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("input[type=file]")).ToHaveCountAsync(0);

        await page.GotoAsync(fixture.BaseUrl + "/Account/Register");
        var id = Guid.NewGuid().ToString("N")[..12];
        await page.Locator("input[autocomplete=username]").FillAsync("demo" + id);
        await page.Locator("input[autocomplete=email]").FillAsync(id + "@example.com");
        foreach (var password in await page.Locator("input[autocomplete=new-password]").AllAsync())
            await password.FillAsync("DemoTestPass123");
        await page.Locator("button[type=submit]").ClickAsync();
        await Assertions.Expect(page.GetByTestId(UiTestIds.OverviewReady)).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId(UiTestIds.DemoBanner)).ToHaveCountAsync(0);
        await Assertions.Expect(page.Locator("img[src^='/images/demo/']")).ToHaveCountAsync(0);
        await page.GotoAsync(fixture.BaseUrl + "/demo");
        await Assertions.Expect(page.GetByTestId(UiTestIds.DemoStartPage)).ToContainTextAsync("privévenster");
        await Assertions.Expect(page.GetByTestId(UiTestIds.DemoStart)).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task OpenCircuit_ExpiresAndCannotSaveAfterOneMinute()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(fixture.BaseUrl + "/demo");
        await Assertions.Expect(page.GetByTestId(UiTestIds.DemoBanner)).ToBeVisibleAsync();
        await page.GotoCreateRecipeAsync(fixture.BaseUrl);
        await page.GetByTestId(UiTestIds.RecipeTitle).FillBlazorInputAsync("Te laat opgeslagen");
        await page.GetByTestId(UiTestIds.IngredientRowName(0)).FillBlazorInputAsync("Wortel");
        await page.GetByTestId(UiTestIds.StepInput(0)).FillBlazorInputAsync("Snijd de wortel.");
        await Assertions.Expect(page.GetByTestId(UiTestIds.DemoBanner)).ToContainTextAsync("demosessie is afgelopen", new() { Timeout = 75_000 });
        await page.GetByTestId(UiTestIds.RecipeSave).ClickAsync();
        await Assertions.Expect(page.GetByText("Je demosessie is afgelopen.", new() { Exact = false }).Last).ToBeVisibleAsync();
        Assert.Contains("/recipes/create", page.Url);
    }
}
