using System.Globalization;
using Azure.Extensions.AspNetCore.DataProtection.Blobs;
using Azure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using RecipeLibrary.Application;
using RecipeLibrary.Application.Contracts;
using RecipeLibrary.Application.Ingredients;
using RecipeLibrary.Application.Security;
using RecipeLibrary.Components;
using RecipeLibrary.Infrastructure.FileStorage;
using RecipeLibrary.Infrastructure.Identity;
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

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddEntityFrameworkStores<RecipeDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/Login";
    options.SlidingExpiration = true;
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.Configure<IdentitySeedUserOptions>(
    builder.Configuration.GetSection(IdentitySeedUserOptions.SectionName));
builder.Services.AddHostedService<IdentitySeedUserHostedService>();

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
var persistenceStartupStartedAt = TimeProvider.System.GetUtcNow();
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
        var durationSeconds = (TimeProvider.System.GetUtcNow() - persistenceStartupStartedAt).TotalSeconds;
        app.Logger.LogError(
            persistenceError,
            "Application failed. StartupDurationSeconds={StartupDurationSeconds:0.###}",
            durationSeconds);
    }
    else
    {
        app.Logger.LogInformation("Application starting (persistence warmup)");
        app.Logger.LogWarning(
            persistenceError,
            "Database is not ready yet (e.g. Azure SQL auto-pause). Serving the starting page until migrations succeed.");
    }
}
else
{
    var durationSeconds = (TimeProvider.System.GetUtcNow() - persistenceStartupStartedAt).TotalSeconds;
    app.Logger.LogInformation(
        "Application ready. StartupDurationSeconds={StartupDurationSeconds:0.###}",
        durationSeconds);
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

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Liveness: process is up (no database dependency). /health is a backward-compatible alias.
static IResult LiveHealth() => Results.Ok(new { status = "Healthy" });
app.MapGet("/health/live", LiveHealth).AllowAnonymous();
app.MapGet("/health", LiveHealth).AllowAnonymous();

// Soft readiness: process can accept HTTP (including /starting) regardless of database state.
app.MapGet("/health/ready", () => Results.Ok(new { status = "Healthy" })).AllowAnonymous();

// Database warmup state for operators/debug (not used by ACA probes).
app.MapGet("/health/database", (IPersistenceReadiness readiness) =>
    Results.Ok(new { state = readiness.State.ToString() })).AllowAnonymous();

// UI poll endpoint while persistence warms up.
app.MapGet("/api/system/readiness", (HttpContext httpContext, IPersistenceReadiness readiness) =>
{
    const int retryAfterSeconds = 5;
    return Results.Ok(new
    {
        state = readiness.State.ToString(),
        retryAfterSeconds = readiness.State == PersistenceWarmupState.Starting ? retryAfterSeconds : (int?)null,
        traceId = httpContext.TraceIdentifier
    });
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
}).DisableAntiforgery().RequireAuthorization();

app.MapGet("/api/recipe-images/{fileName}", async (
    string fileName,
    IQueryBus queryBus,
    IRecipeRepository recipeRepository,
    ICurrentUser currentUser,
    CancellationToken ct) =>
{
    if (string.IsNullOrEmpty(fileName) || fileName.Contains("..", StringComparison.Ordinal) || fileName.IndexOfAny(['/', '\\']) >= 0)
        return Results.NotFound();

    var ownerUserId = currentUser.UserId;
    if (string.IsNullOrWhiteSpace(ownerUserId)
        || !await recipeRepository.IsRecipeImageAccessibleAsync(ownerUserId, fileName, ct))
    {
        return Results.NotFound();
    }

    var query = new GetRecipeImageQuery { StorageKey = fileName };
    var result = await queryBus.QueryAsync<GetRecipeImageQuery, GetRecipeImageResult?>(query, ct);
    if (result is null)
        return Results.NotFound();

    return Results.File(result.Stream, result.ContentType);
}).DisableAntiforgery().RequireAuthorization();

app.MapPost("/ingredients/match", async (MatchIngredientCommand command, ICommandBus commandBus, CancellationToken ct) =>
{
    var result = await commandBus.SendAsync<MatchIngredientCommand, MatchIngredientResult>(command, ct);
    return Results.Ok(result);
}).DisableAntiforgery().RequireAuthorization();

app.MapPost("/ingredients/parse-line", (ParseIngredientLineRequest request, IngredientNameParser parser) =>
{
    var parsed = parser.ParseIngredient(request.Input);
    return Results.Ok(new ParseIngredientLineResult
    {
        Name = parsed.Name,
        Preparation = parsed.Preparation,
    });
}).DisableAntiforgery().RequireAuthorization();

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
}).DisableAntiforgery().RequireAuthorization();

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
}).DisableAntiforgery().RequireAuthorization();

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
}).DisableAntiforgery().RequireAuthorization();

app.MapGet("/ingredients/search", async (string q, string? culture, IQueryBus queryBus, CancellationToken ct) =>
{
    var result = await queryBus.QueryAsync<SearchIngredientsQuery, IReadOnlyList<IngredientLookupItem>>(
        new SearchIngredientsQuery { Query = q, CultureName = culture },
        ct);
    return Results.Ok(result);
}).DisableAntiforgery().RequireAuthorization();

app.MapGet("/tags/search", async (string q, IQueryBus queryBus, CancellationToken ct) =>
{
    var result = await queryBus.QueryAsync<SearchTagsQuery, IReadOnlyList<TagLookupItem>>(
        new SearchTagsQuery { Query = q },
        ct);
    return Results.Ok(result);
}).DisableAntiforgery().RequireAuthorization();

app.MapGet("/culture/set", (string culture, string? redirectUri, HttpContext httpContext) =>
{
    if (culture is not ("nl-NL" or "en-US"))
        return Results.BadRequest();

    var returnPath = LocalRedirect.Normalize(redirectUri);

    httpContext.Response.Cookies.Append(
        CookieRequestCultureProvider.DefaultCookieName,
        CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
        new CookieOptions
        {
            Expires = DateTimeOffset.UtcNow.AddYears(1),
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            HttpOnly = true,
            Secure = httpContext.Request.IsHttps,
        });

    return Results.Redirect(returnPath);
}).AllowAnonymous();

app.MapGet("/measure-system/set", (string system, string? redirectUri, HttpContext httpContext) =>
{
    if (!MeasureSystemService.TryParse(system, out var measureSystem))
        return Results.BadRequest();

    var returnPath = LocalRedirect.Normalize(redirectUri);

    httpContext.Response.Cookies.Append(
        MeasureSystemService.CookieName,
        measureSystem.ToString(),
        MeasureSystemService.CreateCookieOptions(secure: httpContext.Request.IsHttps));

    return Results.Redirect(returnPath);
}).AllowAnonymous();

app.MapGet("/shopping-list/session/set", async (
    Guid groupId,
    string? redirectUri,
    HttpContext httpContext,
    IShoppingListRepository shoppingListRepository,
    ICurrentUser userContext,
    CancellationToken ct) =>
{
    if (groupId == Guid.Empty)
    {
        return Results.BadRequest();
    }

    if (userContext.UserId is not null
        && !await shoppingListRepository.IsGroupAccessibleAsync(groupId, userContext.UserId, ct))
    {
        return Results.Forbid();
    }

    httpContext.Response.Cookies.Append(
        ShoppingListSessionService.GroupIdCookieName,
        groupId.ToString(),
        ShoppingListSessionService.CreateGroupCookieOptions());

    return Results.Redirect(ShoppingListSessionService.NormalizeRedirect(redirectUri));
}).RequireAuthorization();

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
}).RequireAuthorization();

app.MapPost("/Account/Logout", async (SignInManager<ApplicationUser> signInManager) =>
{
    await signInManager.SignOutAsync();
    return Results.LocalRedirect("/Account/Login");
}).AllowAnonymous().DisableAntiforgery();

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
}).DisableAntiforgery().RequireAuthorization();

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


public sealed class AddIngredientTagsRequest
{
    public IReadOnlyList<string> Tags { get; init; } = [];
}

