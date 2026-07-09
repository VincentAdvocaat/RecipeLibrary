using Microsoft.Playwright;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

[Collection(nameof(LocalizationE2eCollection))]
public sealed class LocalizationTests(E2eFixture fixture)
{
    [Fact]
    public async Task LanguageSwitch_NlToEnAndBack_StillFunctional()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoRecipesAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.LanguageSwitcher).ClickAsync();
        await page.GetByTestId(UiTestIds.LanguageEn).ClickAsync();
        await page.GetByTestId(UiTestIds.OverviewReady).WaitForAsync();

        await page.GetByTestId(UiTestIds.LanguageSwitcher).ClickAsync();
        await page.GetByTestId(UiTestIds.LanguageNl).ClickAsync();
        await page.GetByTestId(UiTestIds.OverviewReady).WaitForAsync();

        await Assertions.Expect(page.GetByTestId(UiTestIds.NavCreate)).ToBeVisibleAsync();
    }
}
