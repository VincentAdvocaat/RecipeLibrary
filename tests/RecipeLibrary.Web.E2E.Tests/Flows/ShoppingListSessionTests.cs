using System.Collections.Concurrent;
using Microsoft.Playwright;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

[Collection(nameof(ShoppingListE2eCollection))]
public sealed class ShoppingListSessionTests(E2eFixture fixture)
{
    [Fact]
    public async Task WithoutGroupCookie_NavigationAndListChangesAvoidSessionRedirects()
    {
        await fixture.ResetShoppingStateAsync();
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        var sessionRequests = new ConcurrentQueue<string>();
        page.Request += (_, request) =>
        {
            if (new Uri(request.Url).AbsolutePath.StartsWith("/shopping-list/session/", StringComparison.Ordinal))
            {
                sessionRequests.Enqueue(request.Url);
            }
        };

        await page.GotoRecipesAsync(fixture.BaseUrl);
        Assert.DoesNotContain(await context.CookiesAsync(), cookie => cookie.Name == "ShoppingListGroupId");
        await page.GetByTestId(UiTestIds.NavMenu).ClickAsync();
        await page.GetByTestId(UiTestIds.NavShoppingList).ClickAsync();
        await page.GetByTestId(UiTestIds.ListRename).WaitForAsync();
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });

        var name = $"Cookie-free list {Guid.NewGuid():N}";
        await page.GetByTestId(UiTestIds.ListRename).ClickAsync();
        var renameInput = page.GetByTestId(UiTestIds.ListRenameSave).Locator("..").GetByRole(AriaRole.Textbox);
        await renameInput.FillBlazorInputAsync(name);
        await Assertions.Expect(renameInput).ToHaveValueAsync(name);
        await page.GetByTestId(UiTestIds.ListRenameSave).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = name })).ToBeVisibleAsync();
        Assert.DoesNotContain(await context.CookiesAsync(), cookie => cookie.Name == "ShoppingListGroupId");

        await page.GetByTestId(UiTestIds.ListDelete).ClickAsync();
        await page.GetByTestId(UiTestIds.ConfirmYes).ClickAsync();
        await page.WaitForURLAsync(new Regex("/recipes/?$"));
        await Assertions.Expect(page.GetByTestId(UiTestIds.OverviewReady)).ToBeVisibleAsync();
        Assert.Empty(sessionRequests);
        Assert.DoesNotContain(await context.CookiesAsync(), cookie => cookie.Name == "ShoppingListGroupId");
    }
}
