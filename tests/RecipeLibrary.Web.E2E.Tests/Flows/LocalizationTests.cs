using Microsoft.Playwright;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

[Collection(nameof(LocalizationE2eCollection))]
public sealed class LocalizationTests(E2eFixture fixture)
{
    [Theory]
    [InlineData("nl-NL", UiTestIds.LanguageNl)]
    [InlineData("en-US", UiTestIds.LanguageEn)]
    public async Task SelectingCurrentLanguage_PreservesUnsavedRecipeInput(string culture, string optionTestId)
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/culture/set?culture={culture}&redirectUri=/recipes/{fixture.Seed.RecipeId:D}");
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await page.GetByTestId(UiTestIds.RecipeEdit).ClickAsync();
        var title = page.GetByTestId(UiTestIds.RecipeTitle);
        const string unsavedTitle = "Unsaved recipe title";
        await title.FillBlazorInputAsync(unsavedTitle);
        var navigationRequests = 0;
        page.Request += (_, request) =>
        {
            if (request.IsNavigationRequest)
            {
                navigationRequests++;
            }
        };

        await page.GetByTestId(UiTestIds.LanguageSwitcher).ClickAsync();
        var currentLanguage = page.GetByTestId(optionTestId);
        await Assertions.Expect(currentLanguage).ToHaveAttributeAsync("aria-current", "true");
        await currentLanguage.ClickAsync();

        await Assertions.Expect(title).ToHaveValueAsync(unsavedTitle);
        Assert.Equal(0, navigationRequests);
    }

    [Fact]
    public async Task LanguageSwitch_NlToEnAndBack_StillFunctional()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoRecipesAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.LanguageSwitcher).ClickAsync();
        await page.GetByTestId(UiTestIds.LanguageEn).ClickAsync();
        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("lang", "en");
        await page.GetByTestId(UiTestIds.OverviewReady).WaitForAsync();
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });

        await page.GetByTestId(UiTestIds.LanguageSwitcher).ClickAsync();
        await page.GetByTestId(UiTestIds.LanguageNl).ClickAsync();
        await Assertions.Expect(page.Locator("html")).ToHaveAttributeAsync("lang", "nl");
        await page.GetByTestId(UiTestIds.OverviewReady).WaitForAsync();
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });

        await Assertions.Expect(page.GetByTestId(UiTestIds.AddRecipeFab)).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByTestId(UiTestIds.NavMenu)).ToBeVisibleAsync();
    }
}
