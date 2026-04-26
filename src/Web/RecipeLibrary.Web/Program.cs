using RecipeLibrary.Application;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Components;
using RecipeLibrary.Infrastructure.FileStorage;
using RecipeLibrary.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// In Development, load .env from repo root so MSSQL_SA_PASSWORD is available for local connection string fallback.
if (builder.Environment.IsDevelopment())
{
    DotNetEnv.Env.TraversePath().Load();
}

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var recipeDbConnectionString = builder.Configuration.GetConnectionString("RecipeDb");
if (string.IsNullOrWhiteSpace(recipeDbConnectionString) && builder.Environment.IsDevelopment())
{
    var saPassword = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
    if (!string.IsNullOrWhiteSpace(saPassword))
    {
        recipeDbConnectionString = $"Server=localhost,1433;Database=RecipeLibrary;User Id=sa;Password={saPassword};Encrypt=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
    }
}
if (string.IsNullOrWhiteSpace(recipeDbConnectionString))
{
    throw new InvalidOperationException("Missing connection string 'RecipeDb'. Set ConnectionStrings__RecipeDb (local) or configure it in App Service connection strings.");
}

builder.Services.AddPersistence(recipeDbConnectionString);
builder.Services.AddApplication();

var recipeImagesDefaultPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "RecipeLibraryUploads"));
builder.Services.AddRecipeFileStorage(builder.Configuration, recipeImagesDefaultPath);
builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(nav.BaseUri) };
});

var app = builder.Build();

app.Services.EnsurePersistenceMigrated();

// Developer feedback: log only when DB is unreachable in Development (e.g. SQL container not running).
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<RecipeDbContext>();
        if (!db.Database.CanConnect())
        {
            app.Logger.LogWarning("Cannot connect to RecipeDb. Is the SQL container running?");
        }
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapPost("/api/upload-recipe-image", async (IFormFile file, ICommandBus commandBus, CancellationToken ct) =>
{
    if (file == null || file.Length == 0)
        return Results.BadRequest("No file uploaded.");

    await using var stream = file.OpenReadStream();
    var command = new UploadRecipeImageCommand
    {
        Content = stream,
        FileName = file.FileName,
        ContentType = file.ContentType ?? "application/octet-stream"
    };
    var result = await commandBus.SendAsync<UploadRecipeImageCommand, UploadRecipeImageResult>(command, ct);
    return Results.Ok(new { url = result.Url });
}).DisableAntiforgery();

app.MapGet("/api/recipe-images/{fileName}", async (string fileName, IQueryBus queryBus, CancellationToken ct) =>
{
    if (string.IsNullOrEmpty(fileName) || fileName.Contains("..", StringComparison.Ordinal) || fileName.IndexOfAny(['/', '\\']) >= 0)
        return Results.NotFound();

    var query = new GetRecipeImageQuery { StorageKey = fileName };
    var result = await queryBus.QueryAsync<GetRecipeImageQuery, GetRecipeImageResult?>(query, ct);
    if (result is null)
        return Results.NotFound();

    return Results.File(result.Stream, result.ContentType);
}).DisableAntiforgery();

app.MapPost("/ingredients/match", async (MatchIngredientCommand command, ICommandBus commandBus, CancellationToken ct) =>
{
    var result = await commandBus.SendAsync<MatchIngredientCommand, MatchIngredientResult>(command, ct);
    return Results.Ok(result);
}).DisableAntiforgery();

app.MapGet("/ingredients/search", async (string q, IQueryBus queryBus, CancellationToken ct) =>
{
    var result = await queryBus.QueryAsync<SearchIngredientsQuery, IReadOnlyList<IngredientLookupItem>>(
        new SearchIngredientsQuery { Query = q },
        ct);
    return Results.Ok(result);
}).DisableAntiforgery();

app.MapGet("/tags/search", async (string q, IQueryBus queryBus, CancellationToken ct) =>
{
    var result = await queryBus.QueryAsync<SearchTagsQuery, IReadOnlyList<TagLookupItem>>(
        new SearchTagsQuery { Query = q },
        ct);
    return Results.Ok(result);
}).DisableAntiforgery();

app.MapPost("/ingredients/{id:guid}/tags", async (Guid id, AddIngredientTagsRequest request, ICommandBus commandBus, CancellationToken ct) =>
{
    var result = await commandBus.SendAsync<AddIngredientTagsCommand, AddIngredientTagsResult>(
        new AddIngredientTagsCommand
        {
            IngredientId = id,
            Tags = request.Tags
        },
        ct);
    return Results.Ok(result);
}).DisableAntiforgery();

app.Run();

public sealed class AddIngredientTagsRequest
{
    public IReadOnlyList<string> Tags { get; init; } = [];
}

