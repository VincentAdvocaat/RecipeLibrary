using Microsoft.Playwright;
using RecipeLibrary.Testing;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

[Collection(nameof(RecipeOverviewE2eCollection))]
public sealed class RecipeOverviewTests(E2eFixture fixture)
{
    [Fact]
    public async Task Overview_ShowsSeededRecipe()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoRecipesAsync(fixture.BaseUrl);

        await Assertions.Expect(page.GetByText(TestDataSeeder.LasagnaTitle)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Search_FiltersByTitle()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoRecipesAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.SearchInput).FillBlazorInputAsync("Lasagna");
        await Assertions.Expect(page.GetByText(TestDataSeeder.LasagnaTitle)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task CategoryFilter_Meat_ShowsSeededRecipe()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoRecipesAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.CategoryMeat).ClickAsync();
        await Assertions.Expect(page.GetByText(TestDataSeeder.LasagnaTitle)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ViewToggle_SwitchesToCards()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoRecipesAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.ViewToggleCards).ClickAsync();
        await page.GetByTestId(UiTestIds.RecipeCard(fixture.Seed.RecipeId)).WaitForAsync();
        await Assertions.Expect(page.GetByTestId(UiTestIds.RecipeCard(fixture.Seed.RecipeId))).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ViewToggle_SwitchesToList()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoRecipesAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.ViewToggleCards).ClickAsync();
        await page.GetByTestId(UiTestIds.ViewToggleList).ClickAsync();
        await page.GetByTestId(UiTestIds.RecipeListItem(fixture.Seed.RecipeId)).WaitForAsync();
        await Assertions.Expect(page.GetByTestId(UiTestIds.RecipeListItem(fixture.Seed.RecipeId))).ToBeVisibleAsync();
    }

    [Fact]
    public async Task NavCreate_GoesToCreatePage()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoRecipesAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.NavCreate).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/recipes/create"));
        await Assertions.Expect(page.GetByTestId(UiTestIds.RecipeTitle)).ToBeVisibleAsync();
    }
}
