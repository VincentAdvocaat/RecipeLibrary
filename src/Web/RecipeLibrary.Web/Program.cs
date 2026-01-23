using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.UseCases.Recipes;
using RecipeLibrary.Components;
using RecipeLibrary.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var recipeDbConnectionString =
    builder.Configuration.GetConnectionString("RecipeDb")
    ?? throw new InvalidOperationException("Missing connection string 'RecipeDb'. Set ConnectionStrings__RecipeDb (local) or configure it in App Service connection strings.");

builder.Services.AddPersistence(recipeDbConnectionString);
builder.Services.AddScoped<IRecipeService, CreateRecipeService>();

var app = builder.Build();

app.Services.EnsurePersistenceMigrated();

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

app.Run();

