using RecipeLibrary.Application;
using RecipeLibrary.Components;
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

// Local recipe image upload: save to wwwroot/uploads/recipe-images/ and return URL.
app.MapPost("/api/upload-recipe-image", async (IFormFile file, IWebHostEnvironment env) =>
{
    if (file == null || file.Length == 0)
        return Results.BadRequest("No file uploaded.");

    var allowed = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
    var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
    if (string.IsNullOrEmpty(ext) || !allowed.Contains(ext))
        return Results.BadRequest("Invalid image type. Use jpg, png, gif or webp.");

    var uploadDir = Path.Combine(env.WebRootPath ?? "wwwroot", "uploads", "recipe-images");
    Directory.CreateDirectory(uploadDir);
    var fileName = $"{Guid.NewGuid()}{ext}";
    var filePath = Path.Combine(uploadDir, fileName);
    await using (var stream = File.Create(filePath))
        await file.CopyToAsync(stream);

    var url = $"/uploads/recipe-images/{fileName}";
    return Results.Ok(new { url });
}).DisableAntiforgery();

app.Run();

