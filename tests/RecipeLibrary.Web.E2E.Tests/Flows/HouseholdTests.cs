using Microsoft.Playwright;
using RecipeLibrary.Testing;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

[Collection(nameof(HouseholdE2eCollection))]
public sealed class HouseholdTests(E2eFixture fixture)
{
    [Fact]
    public async Task MembersCanShareAndEditARecipeAndPantry_UntilMembershipIsRemoved()
    {
        await using var managerContext = await fixture.NewAuthenticatedContextAsync();
        await managerContext.GrantPermissionsAsync(["clipboard-read", "clipboard-write"], new() { Origin = fixture.BaseUrl });
        var manager = await managerContext.NewPageAsync();
        manager.UseBlazorDefaults();
        await manager.SetViewportSizeAsync(390, 844);
        await manager.GotoAsync($"{fixture.BaseUrl}/household");
        await WaitForInteractivityAsync(manager);
        await manager.GetByLabel("Naam van het huishouden").FillBlazorInputAsync("Huis aan de Singel");
        await Button(manager, "Maak huishouden aan").ClickAsync();
        await Button(manager, "Maak uitnodigingslink").ClickAsync();
        var invitation = await manager.GetByLabel("Uitnodigingslink", new() { Exact = false }).InputValueAsync();
        await Button(manager, "Kopieer link").ClickAsync();
        await Expect(manager.GetByText("Uitnodigingslink gekopieerd.")).ToBeVisibleAsync();
        Assert.Equal(invitation, await manager.EvaluateAsync<string>("navigator.clipboard.readText()"));

        await manager.GotoRecipesAsync(fixture.BaseUrl);
        await Button(manager, "Delen met huishouden").ClickAsync();
        await manager.GetByTestId(UiTestIds.SearchInput).FillBlazorInputAsync("Lasagna");
        await Expect(manager.GetByRole(AriaRole.Button, new() { NameRegex = new Regex("^Selecteer") }))
            .ToHaveTextAsync("Selecteer 1 resultaat");
        var selectResult = Button(manager, "Selecteer 1 resultaat");
        await Expect(selectResult).ToBeEnabledAsync();
        await selectResult.PressAsync("Enter");
        await Button(manager, "Deel 1 recept").ClickAsync();
        await Expect(manager.GetByText("Al gedeeld", new() { Exact = true })).ToBeVisibleAsync();

        await manager.GetByRole(AriaRole.Link, new() { Name = "Huishouden", Exact = true }).ClickAsync();
        await Expect(manager).ToHaveURLAsync(new Regex("/recipes\\?library=household$"));
        await Expect(manager.GetByTestId(UiTestIds.RecipeListItem(fixture.Seed.RecipeId))).ToHaveCountAsync(0);
        await manager.GetByText(TestDataSeeder.LasagnaTitle, new() { Exact = true }).ClickAsync();
        await Expect(manager.GetByRole(AriaRole.Heading, new() { Name = TestDataSeeder.LasagnaTitle })).ToBeVisibleAsync();
        var sharedRecipeUrl = manager.Url;
        Assert.DoesNotContain(fixture.Seed.RecipeId.ToString(), sharedRecipeUrl);
        await WaitForInteractivityAsync(manager);
        await SetRatingAsync(manager, 4);

        await using var memberContext = await fixture.Browser.NewContextAsync(new() { ViewportSize = new() { Width = 390, Height = 844 } });
        var member = await memberContext.NewPageAsync();
        member.UseBlazorDefaults();
        await member.GotoAsync(invitation);
        await member.GetByRole(AriaRole.Link, new() { Name = "Registreren", Exact = true }).ClickAsync();
        await RegisterAsync(member);
        await WaitForInteractivityAsync(member);
        await Button(member, "Accepteer uitnodiging").ClickAsync();
        await Expect(member.GetByRole(AriaRole.Heading, new() { Name = "Huis aan de Singel" })).ToBeVisibleAsync();

        await member.GotoAsync(sharedRecipeUrl);
        await WaitForInteractivityAsync(member);
        await Expect(member.GetByTestId(UiTestIds.RecipeRatingOption(0))).ToHaveAttributeAsync("aria-pressed", "true");
        await SetRatingAsync(member, 2);
        await member.GetByTestId(UiTestIds.RecipeEdit).ClickAsync();
        await Expect(member.GetByText("Huishouden", new() { Exact = true })).ToBeVisibleAsync();
        const string edited = "Lasagna van het huishouden";
        await member.GetByTestId(UiTestIds.RecipeTitle).FillBlazorInputAsync(edited);
        await member.SaveRecipeAndWaitForDetailAsync();
        await Expect(member.GetByRole(AriaRole.Heading, new() { Name = edited })).ToBeVisibleAsync();
        await manager.GotoAsync(sharedRecipeUrl);
        await Expect(manager.GetByRole(AriaRole.Heading, new() { Name = edited })).ToBeVisibleAsync();
        await Expect(manager.GetByTestId(UiTestIds.RecipeRatingOption(4))).ToHaveAttributeAsync("aria-pressed", "true");
        await WaitForInteractivityAsync(manager);
        await SetRatingAsync(manager, 0);
        await member.ReloadAsync();
        await Expect(member.GetByTestId(UiTestIds.RecipeRatingOption(2))).ToHaveAttributeAsync("aria-pressed", "true");
        await manager.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await Expect(manager.GetByRole(AriaRole.Heading, new() { Name = TestDataSeeder.LasagnaTitle })).ToBeVisibleAsync();

        await member.GotoAsync($"{fixture.BaseUrl}/pantry?library=household");
        await WaitForInteractivityAsync(member);
        var pantryInput = member.GetByTestId(UiTestIds.PantryAddName);
        await pantryInput.FillBlazorInputAsync("Gehakt");
        await Expect(pantryInput).ToHaveValueAsync("Gehakt");
        await member.GetByTestId(UiTestIds.PantryAddSubmit).ClickAsync();
        await Expect(member.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "Gehakt" })).ToBeVisibleAsync();
        await manager.GotoAsync($"{fixture.BaseUrl}/pantry?library=household");
        await Expect(manager.GetByRole(AriaRole.Listitem).Filter(new() { HasText = "Gehakt" })).ToBeVisibleAsync();
        await manager.GotoAsync($"{fixture.BaseUrl}/pantry");
        await manager.GetByTestId(UiTestIds.PantryAddForm).WaitForAsync();
        await Expect(manager.GetByText("Gehakt", new() { Exact = true })).ToHaveCountAsync(0);

        await manager.GotoAsync(sharedRecipeUrl);
        await WaitForInteractivityAsync(manager);
        await manager.GetByTestId(UiTestIds.AddToShoppingList).ClickAsync();
        await Expect(manager.GetByTestId(UiTestIds.RecipeAddedToListStatus)).ToBeVisibleAsync();
        await manager.GotoShoppingListAsync(fixture.BaseUrl);
        await Expect(manager.GetByText("Tomaten", new() { Exact = true })).ToBeVisibleAsync();
        await Expect(manager.GetByText(new Regex("^Gehakt"))).ToHaveCountAsync(0);

        await using var outsiderContext = await fixture.Browser.NewContextAsync();
        var outsider = await outsiderContext.NewPageAsync();
        outsider.UseBlazorDefaults();
        await outsider.GotoAsync($"{fixture.BaseUrl}/Account/Register");
        await RegisterAsync(outsider);
        await outsider.GotoAsync(invitation);
        await WaitForInteractivityAsync(outsider);
        await Button(outsider, "Accepteer uitnodiging").ClickAsync();
        await Expect(outsider.GetByRole(AriaRole.Alert)).ToContainTextAsync("verlopen of al gebruikt");
        await outsider.GotoAsync(sharedRecipeUrl);
        await Expect(outsider).ToHaveURLAsync(new Regex("/not-found$"));

        await manager.GotoAsync($"{fixture.BaseUrl}/household");
        await WaitForInteractivityAsync(manager);
        await Button(manager, "Draag beheer over").ClickAsync();
        await manager.GetByTestId(UiTestIds.ConfirmYes).ClickAsync();
        await Expect(Button(manager, "Verlaat huishouden")).ToBeVisibleAsync();
        await member.GotoAsync($"{fixture.BaseUrl}/household");
        await WaitForInteractivityAsync(member);
        await Button(member, "Draag beheer over").ClickAsync();
        await member.GetByTestId(UiTestIds.ConfirmYes).ClickAsync();
        await Expect(Button(member, "Verlaat huishouden")).ToBeVisibleAsync();
        await manager.ReloadAsync();
        await WaitForInteractivityAsync(manager);
        await Button(manager, "Verwijder lid").ClickAsync();
        await manager.GetByTestId(UiTestIds.ConfirmYes).ClickAsync();
        await Expect(Button(manager, "Verwijder lid")).ToHaveCountAsync(0);
        await member.GotoAsync(sharedRecipeUrl);
        await Expect(member).ToHaveURLAsync(new Regex("/not-found$"));

        await Button(manager, "Maak uitnodigingslink").ClickAsync();
        var newInvitation = manager.GetByLabel("Uitnodigingslink", new() { Exact = false });
        await Expect(newInvitation).Not.ToHaveValueAsync(invitation);
        await outsider.GotoAsync(await newInvitation.InputValueAsync());
        await WaitForInteractivityAsync(outsider);
        await Button(outsider, "Accepteer uitnodiging").ClickAsync();
        await Button(outsider, "Verlaat huishouden").ClickAsync();
        await outsider.GetByTestId(UiTestIds.ConfirmYes).ClickAsync();
        await Expect(Button(outsider, "Maak huishouden aan")).ToBeVisibleAsync();
    }

    private static ILocator Button(IPage page, string name) =>
        page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    private static Task WaitForInteractivityAsync(IPage page) =>
        page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });

    private static async Task SetRatingAsync(IPage page, int stars)
    {
        var option = page.GetByTestId(UiTestIds.RecipeRatingOption(stars));
        await option.ClickAsync();
        await Expect(option).ToHaveAttributeAsync("aria-pressed", "true");
    }

    private static async Task RegisterAsync(IPage page)
    {
        var suffix = Guid.NewGuid().ToString("N")[..12];
        await page.Locator("#register-username").FillAsync("housemate-" + suffix);
        await page.Locator("#register-email").FillAsync(suffix + "@example.invalid");
        await page.Locator("#register-password").FillAsync("E2eTestPass123");
        await page.Locator("#register-confirm").FillAsync("E2eTestPass123");
        await page.Locator("button[type=submit]").ClickAsync();
        await Expect(page).Not.ToHaveURLAsync(new Regex("/Account/Register", RegexOptions.IgnoreCase));
    }
}
