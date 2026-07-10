using Microsoft.Playwright;
using RecipeLibrary.Web.Testing;

namespace RecipeLibrary.Web.E2E.Tests.Helpers;

public static class PlaywrightExtensions
{
    public static void UseBlazorDefaults(this IPage page, int timeoutMs = 60_000)
    {
        page.SetDefaultTimeout(timeoutMs);
    }

    public static async Task GotoRecipesAsync(this IPage page, string baseUrl)
    {
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/recipes");
        await page.GetByTestId(UiTestIds.OverviewReady).WaitForAsync();
    }

    public static async Task GotoCreateRecipeAsync(this IPage page, string baseUrl)
    {
        await page.GotoAsync($"{baseUrl.TrimEnd('/')}/recipes/create");
        await page.GetByTestId(UiTestIds.RecipeTitle).WaitForAsync();
    }

    public static async Task FillBlazorInputAsync(this ILocator locator, string value)
    {
        await locator.ClickAsync();
        await locator.FillAsync(string.Empty);
        await locator.TypeAsync(value, new LocatorTypeOptions { Delay = 20 });
        await locator.PressAsync("Tab");
    }

    public static async Task FillIngredientRowAsync(this IPage page, int index, string name, string? preparation = null, string? quantity = null)
    {
        await page.GetByTestId(UiTestIds.IngredientRowName(index)).FillBlazorInputAsync(name);
        if (preparation is not null)
        {
            await page.GetByTestId(UiTestIds.IngredientRowPreparation(index)).FillBlazorInputAsync(preparation);
        }

        if (quantity is not null)
        {
            await page.Locator("input[type='number']").Nth(index).FillBlazorInputAsync(quantity);
        }
    }

    public static async Task SaveRecipeAndWaitForDetailAsync(this IPage page)
    {
        await page.GetByTestId(UiTestIds.RecipeSave).ClickAsync();
        await page.WaitForURLAsync(new Regex("/recipes/[0-9a-f-]{36}", RegexOptions.IgnoreCase));
    }
}
