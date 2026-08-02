using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Infrastructure.Identity;
using RecipeLibrary.Testing;
using RecipeLibrary.Web.Auth;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

[Collection(nameof(SqlContainerCollection))]
public sealed class ApiV1AuthAndRecipesTests(SqlContainerFixture fixture)
{
    private const string Password = "TestPass1!";

    [Fact]
    public async Task Token_PasswordGrant_Then_ListRecipes_Works()
    {
        await using var factory = new BearerAuthWebApplicationFactory(fixture.ConnectionString);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

        var user = await CreateUserAsync(factory, "apiuser", "apiuser@example.com", Password);
        var token = await RequestPasswordTokenAsync(client, user.UserName!, Password);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var list = await client.GetAsync("/api/v1/recipes");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var create = await client.PostAsJsonAsync("/api/v1/recipes", new CreateRecipeCommand
        {
            Title = "API Pasta",
            PreparationTimeMinutes = 10,
            CookingTimeMinutes = 15,
            Category = 1,
            Difficulty = 1,
            Ingredients = [new CreateRecipeIngredientDto { Name = "Pasta", Quantity = 200, Unit = "Gram" }],
            InstructionSteps = [new CreateRecipeInstructionStepDto { StepNumber = 1, Text = "Boil water." }],
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        var created = await create.Content.ReadFromJsonAsync<CreateRecipeResult>();
        Assert.NotNull(created);

        var get = await client.GetAsync($"/api/v1/recipes/{created.RecipeId}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Recipes_WithoutToken_ReturnsUnauthorized()
    {
        await using var factory = new BearerAuthWebApplicationFactory(fixture.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/recipes");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_Refresh_ReturnsNewAccessToken()
    {
        await using var factory = new BearerAuthWebApplicationFactory(fixture.ConnectionString);
        var client = factory.CreateClient();

        var user = await CreateUserAsync(factory, "refreshuser", "refreshuser@example.com", Password);
        var (access, refresh) = await RequestPasswordTokenPairAsync(client, user.UserName!, Password);
        Assert.False(string.IsNullOrWhiteSpace(access));
        Assert.False(string.IsNullOrWhiteSpace(refresh));

        using var refreshContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refresh!,
            ["client_id"] = OpenIddictAppConstants.MauiClientId,
        });

        var refreshResponse = await client.PostAsync("/connect/token", refreshContent);
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);

        await using var stream = await refreshResponse.Content.ReadAsStreamAsync();
        using var doc = await JsonDocument.ParseAsync(stream);
        Assert.True(doc.RootElement.TryGetProperty("access_token", out var newAccess));
        Assert.False(string.IsNullOrWhiteSpace(newAccess.GetString()));
    }

    [Fact]
    public async Task Register_Then_Token_Works()
    {
        await using var factory = new BearerAuthWebApplicationFactory(fixture.ConnectionString);
        var client = factory.CreateClient();

        var email = $"reg-{Guid.NewGuid():N}@example.com";
        var userName = $"reg{Guid.NewGuid():N}"[..16];
        var register = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            email,
            userName,
            password = Password,
        });
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var token = await RequestPasswordTokenAsync(client, userName, Password);
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    private static async Task<ApplicationUser> CreateUserAsync(
        BearerAuthWebApplicationFactory factory,
        string userName,
        string email,
        string password)
    {
        using var scope = factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            EmailConfirmed = true,
        };
        var result = await userManager.CreateAsync(user, password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return user;
    }

    private static async Task<string> RequestPasswordTokenAsync(HttpClient client, string userName, string password)
    {
        var (access, _) = await RequestPasswordTokenPairAsync(client, userName, password);
        return access;
    }

    private static async Task<(string AccessToken, string? RefreshToken)> RequestPasswordTokenPairAsync(
        HttpClient client,
        string userName,
        string password)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "password",
            ["username"] = userName,
            ["password"] = password,
            ["client_id"] = OpenIddictAppConstants.MauiClientId,
            ["scope"] = $"{OpenIddictAppConstants.ApiScope} offline_access",
        });

        var response = await client.PostAsync("/connect/token", content);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);

        using var doc = JsonDocument.Parse(body);
        var access = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("access_token missing");
        var refresh = doc.RootElement.TryGetProperty("refresh_token", out var refreshElement)
            ? refreshElement.GetString()
            : null;
        return (access, refresh);
    }
}
