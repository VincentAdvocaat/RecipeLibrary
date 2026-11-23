using Microsoft.Playwright;
using RecipeLibrary.Web.E2E.Tests.Fixtures;
using RecipeLibrary.Web.E2E.Tests.Helpers;
using RecipeLibrary.Web.Testing;
using Xunit;
using static Microsoft.Playwright.Assertions;

namespace RecipeLibrary.Web.E2E.Tests.Flows;

[Collection(nameof(RecipeCrudE2eCollection))]
public sealed class RecipeButtonTests(E2eFixture fixture)
{
    [Fact]
    public async Task ImportModes_ValidateLinkAndFillDraftFromTextAndPhoto()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoCreateRecipeAsync(fixture.BaseUrl);
        var processImport = page.GetByRole(AriaRole.Button, new() { Name = "Import verwerken", Exact = true });
        var review = page.GetByText("We hebben dit recept voor je ingevuld.", new() { Exact = false });

        await page.GetByTestId("recipe-create-mode-url").ClickAsync();
        await processImport.ClickAsync();
        await Expect(page.GetByText("Vul een link in om te importeren.")).ToBeVisibleAsync();

        await page.GetByTestId("recipe-create-mode-text").ClickAsync();
        await processImport.ClickAsync();
        await Expect(page.GetByText("Plak recepttekst om te importeren.")).ToBeVisibleAsync();
        await page.Locator("textarea").FillBlazorInputAsync("Tomatensoep\n\nIngrediënten\n200 g tomaten\n\nBereiding\nKook de tomaten.");
        await processImport.ClickAsync();
        await Expect(review).ToBeVisibleAsync();

        await page.GetByTestId("recipe-create-mode-photo").ClickAsync();
        await Expect(processImport).ToBeDisabledAsync();
        var photoInput = page.Locator("input[type=file][multiple]");
        await photoInput.SetInputFilesAsync(new FilePayload { Name = "recipe.txt", MimeType = "text/plain", Buffer = [65] });
        await processImport.ClickAsync();
        await Expect(page.GetByText("Niet-ondersteund afbeeldingstype.", new() { Exact = false })).ToBeVisibleAsync();

        var file = await RecipePhotoAsync(context);
        await photoInput.SetInputFilesAsync(file);
        await processImport.ClickAsync();
        await Expect(page.GetByTestId(UiTestIds.RecipeTitle)).ToBeVisibleAsync(new() { Timeout = 30_000 });
        await Expect(review).ToBeVisibleAsync();

        await page.GetByTestId("recipe-create-mode-manual").ClickAsync();
        await page.GetByTestId(UiTestIds.RecipeCancel).ClickAsync();
        await page.WaitForURLAsync(new Regex("/recipes/?$"));
    }

    [Fact]
    public async Task Editor_CanUploadAndRemoveRecipePhotoAndAddAndRemoveStep()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoCreateRecipeAsync(fixture.BaseUrl);
        var file = await RecipePhotoAsync(context);
        var uploadZone = page.GetByText("Klik om een foto toe te voegen of sleep een afbeelding hierheen").Locator("../..");
        var chooser = await page.RunAndWaitForFileChooserAsync(() => uploadZone.ClickAsync());
        await chooser.SetFilesAsync(file);
        var photo = page.GetByAltText("Recept foto");
        await Expect(photo).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Foto verwijderen" }).ClickAsync();
        await Expect(photo).ToHaveCountAsync(0);

        await page.GetByTestId(UiTestIds.StepAdd).ClickAsync();
        var secondStep = page.GetByTestId(UiTestIds.StepInput(1));
        await Expect(secondStep).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Verwijderen", Exact = true }).Last.ClickAsync();
        await Expect(secondStep).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Detail_CanSwitchUnitsReportAndSignOut()
    {
        await using var context = await fixture.NewAuthenticatedContextAsync();
        var page = await context.NewPageAsync();
        page.UseBlazorDefaults();
        await page.GotoAsync($"{fixture.BaseUrl}/recipes/{fixture.Seed.RecipeId}");
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        var units = page.GetByTestId(UiTestIds.MeasureSystemSwitcher);
        await units.ClickAsync();
        await page.GetByTestId(UiTestIds.MeasureSystemImperial).ClickAsync();
        await Expect(units).ToContainTextAsync("Imperial");
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await units.ClickAsync();
        await page.GetByTestId(UiTestIds.MeasureSystemMetric).ClickAsync();
        await Expect(units).ToContainTextAsync("Metriek");
        await page.GetByTestId(UiTestIds.AppInteractiveReady).WaitForAsync(new() { State = WaitForSelectorState.Attached });
        await page.GetByTestId(UiTestIds.RecipeReport).ClickAsync();
        await Expect(page.GetByText("je melding is ingediend voor beoordeling.", new() { Exact = false })).ToBeVisibleAsync();
        await page.GetByTestId(UiTestIds.AccountMenu).ClickAsync();
        await page.GetByTestId(UiTestIds.AccountMenuSignOut).ClickAsync();
        await page.GotoAsync(fixture.BaseUrl + "/recipes");
        await Expect(page.Locator("#login-email")).ToBeVisibleAsync();
    }

    private static async Task<FilePayload> RecipePhotoAsync(IBrowserContext context)
    {
        var source = await context.NewPageAsync();
        try
        {
            await source.SetContentAsync("""
                <div style="width:720px;padding:32px;font:30px Arial;color:black;background:white">
                  <h1>Tomatensoep</h1>
                  <p>Ingrediënten<br>200 g tomaten<br>500 ml water</p>
                  <p>Bereiding<br>Kook de tomaten. Voeg water toe.</p>
                </div>
                """);
            return new FilePayload
            {
                Name = "recipe.png",
                MimeType = "image/png",
                Buffer = await source.Locator("div").ScreenshotAsync(),
            };
        }
        finally
        {
            await source.CloseAsync();
        }
    }
}
