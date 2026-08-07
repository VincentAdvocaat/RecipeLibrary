using RecipeLibrary.App.Services;

namespace RecipeLibrary.App;

public partial class MainPage : ContentPage
{
    private readonly RecipeApiClient _api;

    public MainPage(RecipeApiClient api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void OnLoginClicked(object? sender, EventArgs e)
    {
        try
        {
            StatusLabel.Text = "Signing in...";
            await _api.LoginAsync(UserNameEntry.Text?.Trim() ?? string.Empty, PasswordEntry.Text ?? string.Empty);
            StatusLabel.Text = "Loading recipes...";
            var recipes = await _api.GetRecipesAsync();
            RecipesList.ItemsSource = recipes;
            StatusLabel.Text = $"Loaded {recipes.Count} recipe(s).";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = ex.Message;
        }
    }
}
