using System.Globalization;
using Azure.Extensions.AspNetCore.DataProtection.Blobs;
using Azure.Identity;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.Identity.Web;
using Microsoft.Identity.Web.UI;
using RecipeLibrary.Application;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Components;
using RecipeLibrary.Infrastructure.FileStorage;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Infrastructure.RecipeImport;
using RecipeLibrary.Application.Abstractions;
using RecipeLibrary.Web.Services;
using RecipeLibrary.Web.Middleware;

var builder = WebApplication.CreateBuilder(args);

// In Development, load .env from repo root so MSSQL_SA_PASSWORD is available for local connection string fallback.
if (builder.Environment.IsDevelopment())
{
    LoadDotEnvFromAncestors(builder.Environment.ContentRootPath);
}

static void LoadDotEnvFromAncestors(string startPath)
{
    for (var dir = new DirectoryInfo(startPath); dir is not null; dir = dir.Parent)
    {
        var envFile = Path.Combine(dir.FullName, ".env");
        if (!File.Exists(envFile))
        {
            continue;
        }

        DotNetEnv.Env.Load(envFile);
        return;
    }

    DotNetEnv.Env.TraversePath().Load();
}

// Testing uses appsettings.Testing.json and WebApplicationFactory overrides.

// Add services to the container.
builder.Services.AddLocalization();

var supportedCultures = new[] { new CultureInfo("nl-NL"), new CultureInfo("en-US") };
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    options.DefaultRequestCulture = new RequestCulture("nl-NL");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.RequestCultureProviders =
    [
        new CookieRequestCultureProvider(),
        new AcceptLanguageHeaderRequestCultureProvider()
    ];
});

var azureAdSection = builder.Configuration.GetSection("AzureAd");
var authEnabled = !string.IsNullOrWhiteSpace(azureAdSection["ClientId"]);

builder.Services.AddSingleton(new AuthFeatureOptions { IsEnabled = authEnabled });

if (authEnabled)
{
    builder.Services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApp(azureAdSection);
    builder.Services.AddControllersWithViews()
        .AddMicrosoftIdentityUI();
    builder.Services.AddCascadingAuthenticationState();
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
    builder.Services.AddScoped<IShoppingListUserContext, HttpShoppingListUserContext>();
}
else
{
    builder.Services.AddScoped<IShoppingListUserContext, AnonymousShoppingListUserContext>();
}

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Browser file uploads travel over SignalR; raise the limit for multi-screenshot OCR import.
builder.Services.Configure<Microsoft.AspNetCore.SignalR.HubOptions>(options =>
{
    options.MaximumReceiveMessageSize = 32 * 1024 * 1024;
});

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
    throw new InvalidOperationException(
        "Missing connection string 'RecipeDb'. For local development: set ASPNETCORE_ENVIRONMENT=Development, " +
        "add MSSQL_SA_PASSWORD to .env at the repository root, and start the SQL container (e.g. rlstart). " +
        "Or set ConnectionStrings__RecipeDb explicitly.");
}

builder.Services.AddPersistence(recipeDbConnectionString);
builder.Services.AddHostedService<PersistenceWarmupHostedService>();
builder.Services.AddRecipeImport(builder.Configuration);
builder.Services.AddApplication();

var ocrOptions = builder.Configuration.GetSection($"{RecipeImportOptions.SectionName}:Ocr").Get<RecipeImportOcrOptions>()
    ?? new RecipeImportOcrOptions();
builder.Services.Configure<FormOptions>(options =>
{
    var maxImages = Math.Max(1, ocrOptions.MaxImagesPerImport);
    options.MultipartBodyLengthLimit = (long)ocrOptions.MaxImageBytes * maxImages + (256 * 1024);
});

var recipeImagesDefaultPath = Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "RecipeLibraryUploads"));
builder.Services.AddRecipeFileStorage(builder.Configuration, recipeImagesDefaultPath);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ShoppingListSessionService>();
builder.Services.AddScoped<PantrySessionService>();
builder.Services.AddScoped<MeasureSystemService>();

ConfigureDataProtection(builder);

builder.Services.AddScoped(sp =>
{
    var nav = sp.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>();
    // Blazor Server loopback HttpClient does not forward the browser culture cookie.
    // Accept-Language lets RequestLocalization set CurrentUICulture for ingredient i18n APIs.
    var client = new HttpClient
    {
        BaseAddress = new Uri(nav.BaseUri),
        // OCR of multiple screenshots can take longer than the default 100s.
        Timeout = TimeSpan.FromMinutes(5),
    };
    var uiCulture = CultureInfo.CurrentUICulture.Name;
    if (!string.IsNullOrWhiteSpace(uiCulture))
    {
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd(uiCulture);
    }

    return client;
});

var app = builder.Build();

// Prefer a synchronous migrate on startup. When Azure SQL is paused (error 40613), continue so the
// app can serve a "starting" page while PersistenceWarmupHostedService retries in the background.
if (app.Environment.IsEnvironment("Testing"))
{
    app.Services.EnsurePersistenceMigrated();
    app.Services.GetRequiredService<IPersistenceReadiness>().MarkReady();
}
else if (!app.Services.TryEnsurePersistenceMigrated(out var persistenceError))
{
    var readiness = app.Services.GetRequiredService<IPersistenceReadiness>();
    if (readiness.HasPermanentlyFailed)
    {
        app.Logger.LogError(
            persistenceError,
            "Database migration failed with a non-transient error. Serving the failed starting page.");
    }
    else
    {
        app.Logger.LogWarning(
            persistenceError,
            "Database is not ready yet (e.g. Azure SQL auto-pause). Serving the starting page until migrations succeed.");
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

var forwardedHeadersOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<ForwardedHeadersOptions>>().Value;
forwardedHeadersOptions.KnownIPNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders();

app.UseHttpsRedirection();

var localizationOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

// Before auth: otherwise FallbackPolicy challenges to login while SQL is still waking up.
app.UseMiddleware<PersistenceReadinessMiddleware>();

if (authEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UseAntiforgery();

app.MapStaticAssets();

if (authEnabled)
{
    app.MapControllers();
}

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Liveness/startup: always 200 so Container Apps keeps the revision alive during SQL resume.
app.MapGet("/health", (IPersistenceReadiness readiness) =>
{
    var database = readiness.IsReady
        ? "Ready"
        : readiness.HasPermanentlyFailed
            ? "Failed"
            : "Starting";

    return Results.Ok(new
    {
        status = "Healthy",
        database
    });
}).AllowAnonymous();

// Readiness: only Ready gets traffic; Starting/Failed return 503 so probes stop routing.
app.MapGet("/health/ready", (IPersistenceReadiness readiness) =>
{
    if (readiness.IsReady)
    {
        return Results.Ok(new { status = "Healthy", database = "Ready" });
    }

    var database = readiness.HasPermanentlyFailed ? "Failed" : "Starting";
    return Results.Json(
        new { status = "Unavailable", database },
        statusCode: StatusCodes.Status503ServiceUnavailable);
}).AllowAnonymous();

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
}).DisableAntiforgery().ApplyAuth(authEnabled);

app.MapGet("/api/recipe-images/{fileName}", async (string fileName, IQueryBus queryBus, CancellationToken ct) =>
{
    if (string.IsNullOrEmpty(fileName) || fileName.Contains("..", StringComparison.Ordinal) || fileName.IndexOfAny(['/', '\\']) >= 0)
        return Results.NotFound();

    var query = new GetRecipeImageQuery { StorageKey = fileName };
    var result = await queryBus.QueryAsync<GetRecipeImageQuery, GetRecipeImageResult?>(query, ct);
    if (result is null)
        return Results.NotFound();

    return Results.File(result.Stream, result.ContentType);
}).DisableAntiforgery().ApplyAuth(authEnabled);

app.MapPost("/ingredients/match", async (MatchIngredientCommand command, ICommandBus commandBus, CancellationToken ct) =>
{
    var result = await commandBus.SendAsync<MatchIngredientCommand, MatchIngredientResult>(command, ct);
    return Results.Ok(result);
}).DisableAntiforgery().ApplyAuth(authEnabled);

app.MapPost("/ingredients/parse-line", (ParseIngredientLineRequest request, IngredientNameParser parser) =>
{
    var parsed = parser.ParseIngredient(request.Input);
    return Results.Ok(new ParseIngredientLineResult
    {
        Name = parsed.Name,
        Preparation = parsed.Preparation,
    });
}).DisableAntiforgery().ApplyAuth(authEnabled);

app.MapPost("/recipes/import", async (ImportRecipeContentQuery query, IQueryBus queryBus, CancellationToken ct) =>
{
    try
    {
        var result = await queryBus.QueryAsync<ImportRecipeContentQuery, ImportRecipeResult>(query, ct);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ex.Message);
    }
}).DisableAntiforgery().ApplyAuth(authEnabled);

app.MapPost("/recipes/import-url", async (ImportRecipeFromUrlQuery query, IQueryBus queryBus, CancellationToken ct) =>
{
    try
    {
        var result = await queryBus.QueryAsync<ImportRecipeFromUrlQuery, ImportRecipeResult>(query, ct);
        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ex.Message);
    }
}).DisableAntiforgery().ApplyAuth(authEnabled);

app.MapPost("/recipes/import-image", async (
    HttpRequest request,
    IQueryBus queryBus,
    Microsoft.Extensions.Options.IOptions<RecipeImportOptions> importOptions,
    CancellationToken ct) =>
{
    try
    {
        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Expected multipart form data.");
        }

        var ocr = importOptions.Value.Ocr;
        var maxBytes = ocr.MaxImageBytes;
        var maxImages = Math.Max(1, ocr.MaxImagesPerImport);
        var form = await request.ReadFormAsync(ct);
        IReadOnlyList<IFormFile> files = form.Files.GetFiles("file");
        if (files.Count == 0)
        {
            files = form.Files.ToList();
        }

        if (files.Count == 0)
        {
            return Results.BadRequest("Image file is required.");
        }

        if (files.Count > maxImages)
        {
            return Results.BadRequest($"At most {maxImages} images are allowed per import.");
        }

        var images = new List<ImportImageFile>(files.Count);
        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                return Results.BadRequest("Image file is required.");
            }

            if (file.Length > maxBytes)
            {
                return Results.BadRequest($"Image exceeds maximum size of {maxBytes} bytes.");
            }

            await using var stream = file.OpenReadStream();
            using var memory = new MemoryStream(capacity: (int)Math.Min(file.Length, maxBytes));
            await stream.CopyToAsync(memory, ct);
            images.Add(new ImportImageFile
            {
                ImageBytes = memory.ToArray(),
                ContentType = file.ContentType ?? string.Empty,
                FileName = file.FileName ?? string.Empty,
            });
        }

        var language = form["language"].ToString();
        var useAiFallback = !string.Equals(form["useAiFallback"].ToString(), "false", StringComparison.OrdinalIgnoreCase);
        var useFullRecipeAi = string.Equals(form["useFullRecipeAi"].ToString(), "true", StringComparison.OrdinalIgnoreCase);
        var result = await queryBus.QueryAsync<ImportRecipeFromImageQuery, ImportRecipeResult>(
            new ImportRecipeFromImageQuery
            {
                Images = images,
                Language = string.IsNullOrWhiteSpace(language) ? "nld" : language,
                ParseOptions = new ImportRecipeParseOptions
                {
                    UseAiFallback = useAiFallback,
                    UseFullRecipeAi = useFullRecipeAi,
                },
            },
            ct);

        return Results.Ok(result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ex.Message);
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ex.Message);
    }
}).DisableAntiforgery().ApplyAuth(authEnabled);

app.MapGet("/ingredients/search", async (string q, string? culture, IQueryBus queryBus, CancellationToken ct) =>
{
    var result = await queryBus.QueryAsync<SearchIngredientsQuery, IReadOnlyList<IngredientLookupItem>>(
        new SearchIngredientsQuery { Query = q, CultureName = culture },
        ct);
    return Results.Ok(result);
}).DisableAntiforgery().ApplyAuth(authEnabled);

app.MapGet("/tags/search", async (string q, IQueryBus queryBus, CancellationToken ct) =>
{
    var result = await queryBus.QueryAsync<SearchTagsQuery, IReadOnlyList<TagLookupItem>>(
        new SearchTagsQuery { Query = q },
        ct);
    return Results.Ok(result);
}).DisableAntiforgery().ApplyAuth(authEnabled);

app.MapGet("/culture/set", (string culture, string? redirectUri, HttpContext httpContext) =>
{
    if (culture is not ("nl-NL" or "en-US"))
        return Results.BadRequest();

    var returnPath = string.IsNullOrWhiteSpace(redirectUri) || !redirectUri.StartsWith('/')
        ? "/"
        : redirectUri;

    httpContext.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true
        });

    return Results.Redirect(returnPath);
}).AllowAnonymous();

app.MapGet("/measure-system/set", (string system, string? redirectUri, HttpContext httpContext) =>
{
    if (!MeasureSystemService.TryParse(system, out var measureSystem))
        return Results.BadRequest();

    var returnPath = string.IsNullOrWhiteSpace(redirectUri) || !redirectUri.StartsWith('/')
        ? "/"
        : redirectUri;

    httpContext.Response.Cookies.Append(
        MeasureSystemService.CookieName,
        measureSystem.ToString(),
        MeasureSystemService.CreateCookieOptions());

    return Results.Redirect(returnPath);
}).AllowAnonymous();

app.MapGet("/shopping-list/session/set", async (
    Guid groupId,
    string? redirectUri,
    HttpContext httpContext,
    IShoppingListRepository shoppingListRepository,
    IShoppingListUserContext userContext,
    CancellationToken ct) =>
{
    if (groupId == Guid.Empty)
    {
        return Results.BadRequest();
    }

    if (userContext.OwnerUserId is not null
        && !await shoppingListRepository.IsGroupAccessibleAsync(groupId, userContext.OwnerUserId, ct))
    {
        return Results.Forbid();
    }

    httpContext.Response.Cookies.Append(
        ShoppingListSessionService.GroupIdCookieName,
        groupId.ToString(),
        ShoppingListSessionService.CreateGroupCookieOptions());

    return Results.Redirect(ShoppingListSessionService.NormalizeRedirect(redirectUri));
}).ApplyAuth(authEnabled);

app.MapGet("/shopping-list/session/clear", (string? redirectUri, HttpContext httpContext) =>
{
    httpContext.Response.Cookies.Delete(
        ShoppingListSessionService.GroupIdCookieName,
        new CookieOptions
        {
            SameSite = SameSiteMode.Lax,
            HttpOnly = true,
            Path = "/",
        });

    return Results.Redirect(ShoppingListSessionService.NormalizeRedirect(redirectUri));
}).ApplyAuth(authEnabled);

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
}).DisableAntiforgery().ApplyAuth(authEnabled);

app.Run();

static void ConfigureDataProtection(WebApplicationBuilder builder)
{
    var blobUri = builder.Configuration["DataProtection:BlobUri"];
    if (string.IsNullOrWhiteSpace(blobUri))
    {
        return;
    }

    var applicationName = builder.Configuration["DataProtection:ApplicationName"] ?? "RecipeLibrary";
    builder.Services
        .AddDataProtection()
        .SetApplicationName(applicationName)
        .PersistKeysToAzureBlobStorage(new Uri(blobUri), new DefaultAzureCredential());
}

public partial class Program { }

file static class AuthEndpointExtensions
{
    public static RouteHandlerBuilder ApplyAuth(this RouteHandlerBuilder builder, bool authEnabled) =>
        authEnabled ? builder.RequireAuthorization() : builder;
}

public sealed class AddIngredientTagsRequest
{
    public IReadOnlyList<string> Tags { get; init; } = [];
}

