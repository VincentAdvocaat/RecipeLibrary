using Microsoft.Playwright;
using RecipeLibrary.Testing;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;

[Collection(nameof(ShoppingListE2eCollection))]
public sealed class ShoppingListTests(E2eFixture fixture)
{
    [Fact]
    public async Task Detail_AddToShoppingList_ShowsItemOnListPage()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");

        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/shopping-list");
        await page.GetByTestId(UiTestIds.ShoppingListReady).WaitForAsync();

        await Assertions.Expect(page.GetByText("Gehakt")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Overview_MultiSelect_AddsToList()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoRecipesAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.SelectModeStart).ClickAsync();
        await page.GetByTestId(UiTestIds.SelectRecipe(fixture.Seed.RecipeId)).CheckAsync();
        await page.GetByTestId(UiTestIds.AddSelectedToList).ClickAsync();

        await page.GotoAsync($"{fixture.BaseUrl}/shopping-list");
        await page.GetByTestId(UiTestIds.ShoppingListReady).WaitForAsync();
        await Assertions.Expect(page.GetByText("Gehakt")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ToggleItem_ChecksOff()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/shopping-list");
        await page.GetByTestId(UiTestIds.ShoppingListReady).WaitForAsync();

        var checkbox = page.Locator("[data-testid$='-checkbox']").First;
        await checkbox.CheckAsync();
        await Assertions.Expect(checkbox).ToBeCheckedAsync();
    }

    [Fact]
    public async Task RenameList_UpdatesHeading()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/shopping-list");
        await page.GetByTestId(UiTestIds.ShoppingListReady).WaitForAsync();

        var newName = $"List {Guid.NewGuid():N}";
        await page.GetByTestId(UiTestIds.ListRename).ClickAsync();
        await page.Locator("input").First.FillBlazorInputAsync(newName);
        await page.GetByTestId(UiTestIds.ListRenameSave).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = newName })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SplitList_CreatesSecondTab()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/shopping-list");
        await page.GetByTestId(UiTestIds.ShoppingListReady).WaitForAsync();

        await page.GetByTestId(UiTestIds.SplitStart).ClickAsync();
        await page.Locator("input[type=checkbox]").First.CheckAsync();
        await page.GetByTestId(UiTestIds.SplitCreateFromSelection).ClickAsync();

        var newListName = $"Store {Guid.NewGuid():N}";
        await page.Locator(".fixed input").FillBlazorInputAsync(newListName);
        await page.GetByTestId(UiTestIds.SplitConfirm).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = newListName }).WaitForAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = newListName })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task RemoveItem_RemovesFromList()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/shopping-list");
        await page.GetByTestId(UiTestIds.ShoppingListReady).WaitForAsync();

        var removeButtons = page.Locator("[data-testid$='-remove']");
        var countBefore = await removeButtons.CountAsync();
        await removeButtons.First.ClickAsync();
        await Assertions.Expect(removeButtons).ToHaveCountAsync(countBefore - 1);
    }

    [Fact]
    public async Task ClearList_WithConfirm_EmptiesList()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/shopping-list");
        await page.GetByTestId(UiTestIds.ShoppingListReady).WaitForAsync();

        await page.GetByTestId(UiTestIds.ListClear).ClickAsync();
        await page.GetByTestId(UiTestIds.ConfirmYes).WaitForAsync();
        await page.GetByTestId(UiTestIds.ConfirmYes).ClickAsync();

        await Assertions.Expect(page.GetByText("Gehakt")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task EditQuantity_UpdatesDisplayedAmount()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GotoAsync($"{fixture.BaseUrl}/shopping-list");
        await page.GetByTestId(UiTestIds.ShoppingListReady).WaitForAsync();

        var editButton = page.Locator("[data-testid$='-quantity-edit']").First;
        await editButton.ClickAsync();
        var quantityInput = page.Locator("[data-testid$='-quantity-input'] input").First;
        await quantityInput.FillAsync("9");
        await page.Locator("[data-testid$='-quantity-save']").First.ClickAsync();

        await Assertions.Expect(page.GetByText("9")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task AddManualItem_ShowsOnList()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/shopping-list");
        await page.GetByTestId(UiTestIds.ShoppingListReady).WaitForAsync();

        var itemName = $"Handmatig {Guid.NewGuid():N}";
        await page.GetByTestId(UiTestIds.AddItemName).FillBlazorInputAsync(itemName);
        await page.GetByTestId(UiTestIds.AddItemSubmit).ClickAsync();

        await Assertions.Expect(page.GetByText(itemName)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PantryPage_AddItem_ShowsOnList()
    {
        await using var context = await fixture.Browser.NewContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/pantry");
        await page.GetByTestId(UiTestIds.PantryReady).WaitForAsync();

        var itemName = $"Voorraad {Guid.NewGuid():N}";
        await page.GetByTestId(UiTestIds.PantryAddName).FillBlazorInputAsync(itemName);
        await page.GetByTestId(UiTestIds.PantryAddSubmit).ClickAsync();

        await Assertions.Expect(page.GetByText(itemName)).ToBeVisibleAsync();
    }
}
