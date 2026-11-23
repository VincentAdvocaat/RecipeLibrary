using Microsoft.Playwright;
using RecipeLibrary.Testing;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;

[Collection(nameof(ShoppingListE2eCollection))]
public sealed class ShoppingListTests(E2eFixture fixture, Xunit.Abstractions.ITestOutputHelper output) : IAsyncLifetime
{
    public Task InitializeAsync() => fixture.ResetShoppingStateAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Detail_AddToShoppingList_ShowsItemOnListPage()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });

        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GetByTestId(UiTestIds.RecipeAddedToListStatus).WaitForAsync();
        await page.GotoShoppingListAsync(fixture.BaseUrl);

        await Assertions.Expect(page.GetByText("Gehakt")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task Overview_MultiSelect_AddsToList()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoRecipesAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.SelectModeStart).ClickAsync();
        await page.GetByTestId(UiTestIds.SelectRecipe(fixture.Seed.RecipeId)).CheckAsync();
        await page.GetByTestId(UiTestIds.AddSelectedToList).ClickAsync();
        await page.GetByTestId(UiTestIds.RecipeSelectionStatus).WaitForAsync();

        await page.GotoShoppingListAsync(fixture.BaseUrl);
        await Assertions.Expect(page.GetByText("Gehakt")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task ToggleItem_ChecksOff()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GetByTestId(UiTestIds.RecipeAddedToListStatus).WaitForAsync();
        await page.GotoShoppingListAsync(fixture.BaseUrl);

        var checkbox = page.Locator("[data-testid$='-checkbox']").First;
        await checkbox.CheckAsync();
        await Assertions.Expect(checkbox).ToBeCheckedAsync();
    }

    [Fact]
    public async Task RenameList_UpdatesHeading()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoShoppingListAsync(fixture.BaseUrl);

        var newName = $"List {Guid.NewGuid():N}";
        await page.GetByTestId(UiTestIds.ListRename).ClickAsync();
        await page.Locator("input").First.FillBlazorInputAsync(newName);
        await page.GetByTestId(UiTestIds.ListRenameSave).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = newName })).ToBeVisibleAsync();
    }

    [Fact]
    public async Task SplitList_MoveItemsAndDeleteAllLists()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GetByTestId(UiTestIds.RecipeAddedToListStatus).WaitForAsync();
        await page.GotoShoppingListAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.SplitStart).ClickAsync();
        var splitButton = page.GetByTestId(UiTestIds.SplitCreateFromSelection);
        await page.GetByRole(AriaRole.Button, new() { Name = "Alles selecteren" }).ClickAsync();
        await Assertions.Expect(splitButton).ToBeEnabledAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Selectie wissen" }).ClickAsync();
        await Assertions.Expect(splitButton).ToBeDisabledAsync();
        var meat = page.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "Gehakt" });
        await meat.GetByRole(AriaRole.Checkbox).CheckAsync();
        await splitButton.ClickAsync();

        var newListName = $"Store {Guid.NewGuid():N}";
        await page.Locator(".fixed input").FillBlazorInputAsync(newListName);
        await page.GetByTestId(UiTestIds.SplitConfirm).ClickAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = newListName }).WaitForAsync();
        await meat.Locator("[data-testid$='-move']").ClickAsync();
        await Assertions.Expect(meat).ToHaveCountAsync(0);
        await page.Locator("[data-testid^='list-tab-']").First.ClickAsync();
        await meat.Locator("[data-testid$='-move-to-pantry']").ClickAsync();
        await Assertions.Expect(meat).ToHaveCountAsync(0);

        await page.GetByTestId(UiTestIds.AddItemName).FillAsync("Gehakt");
        await page.GetByRole(AriaRole.Button, new() { Name = "Gehakt", Exact = true }).ClickAsync();
        await page.GetByTestId(UiTestIds.AddItemSubmit).ClickAsync();
        await Assertions.Expect(meat).ToBeVisibleAsync();
        await page.GetByTestId(UiTestIds.ApplyPantry).ClickAsync();
        await Assertions.Expect(meat).ToHaveCountAsync(0);

        await page.GetByRole(AriaRole.Button, new() { Name = "Alle lijsten verwijderen" }).ClickAsync();
        await page.GetByTestId(UiTestIds.ConfirmYes).ClickAsync();
        await page.WaitForURLAsync(new Regex("/recipes/?$"));
        await page.GotoAsync(fixture.BaseUrl + "/pantry");
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var pantryRemoval = meat.Locator("[data-testid$='-remove']");
        await pantryRemoval.ClickAsync();
        await Assertions.Expect(meat).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task RemoveItem_RemovesFromList()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GetByTestId(UiTestIds.RecipeAddedToListStatus).WaitForAsync();
        await page.GotoShoppingListAsync(fixture.BaseUrl);

        var removeButtons = page.Locator("[data-testid$='-remove']");
        await Assertions.Expect(removeButtons.First).ToBeVisibleAsync();
        var countBefore = await removeButtons.CountAsync();
        await removeButtons.First.ClickAsync();
        await Assertions.Expect(removeButtons).ToHaveCountAsync(countBefore - 1);
    }

    [Fact]
    public async Task ClearList_WithConfirm_EmptiesList()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GetByTestId(UiTestIds.RecipeAddedToListStatus).WaitForAsync();
        await page.GotoShoppingListAsync(fixture.BaseUrl);

        await page.GetByTestId(UiTestIds.ListClear).ClickAsync();
        await page.GetByTestId(UiTestIds.ConfirmYes).WaitForAsync();
        await page.GetByTestId(UiTestIds.ConfirmYes).ClickAsync();

        await Assertions.Expect(page.GetByText("Gehakt")).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task EditQuantity_UpdatesDisplayedAmount()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await page.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await page.GetByTestId(UiTestIds.RecipeAddedToListStatus).WaitForAsync();
        await page.GotoShoppingListAsync(fixture.BaseUrl);

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
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoShoppingListAsync(fixture.BaseUrl);

        var itemName = $"Handmatig {Guid.NewGuid():N}";
        await page.GetByTestId(UiTestIds.AddItemName).FillBlazorInputAsync(itemName);
        await page.GetByTestId(UiTestIds.AddItemSubmit).ClickAsync();

        await Assertions.Expect(page.GetByText(itemName)).ToBeVisibleAsync();
    }

    [Fact]
    public async Task PantryPage_AddItem_ShowsOnList()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.Console += (_, message) => output.WriteLine("Browser: " + message.Text);
        page.PageError += (_, error) => output.WriteLine("Browser error: " + error);
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/pantry");
        await page.GetByTestId(UiTestIds.PantryReady).WaitForAsync();
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });

        var itemName = $"Voorraad {Guid.NewGuid():N}";
        try
        {
            await page.GetByTestId(UiTestIds.PantryAddName).FillBlazorInputAsync(itemName);
            await Assertions.Expect(page.GetByTestId(UiTestIds.PantryAddName)).ToHaveValueAsync(itemName);
            await page.GetByTestId(UiTestIds.PantryAddSubmit).ClickAsync();
            await Assertions.Expect(page.GetByText(itemName)).ToBeVisibleAsync();
        }
        catch
        {
            output.WriteLine("Pantry input: " + await page.GetByTestId(UiTestIds.PantryAddName).InputValueAsync());
            output.WriteLine(await page.Locator("body").InnerTextAsync());
            output.WriteLine(fixture.Diagnostics);
            var directory = Path.Combine(AppContext.BaseDirectory, "TestResults");
            Directory.CreateDirectory(directory);
            await page.ScreenshotAsync(new() { Path = Path.Combine(directory, "pantry-failure.png"), FullPage = true });
            throw;
        }
    }
}
