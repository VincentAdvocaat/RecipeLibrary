using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RecipeLibrary.App.Services;

namespace RecipeLibrary.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Prefer embedded appsettings.json (filesystem MauiAsset is often absent on device).
        // Keep the stream open until Build() so the JSON provider can load it.
        var embeddedSettings = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("RecipeLibrary.App.appsettings.json");
        if (embeddedSettings is not null)
        {
            builder.Configuration.AddJsonStream(embeddedSettings);
        }

        builder.Configuration.AddJsonFile("appsettings.json", optional: true);

#if DEBUG
        builder.Logging.AddDebug();
#endif

        var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7199/";
        builder.Services.AddSingleton(_ =>
        {
            var handler = new HttpClientHandler();
#if DEBUG
            // Local HTTPS certificates are often untrusted on simulators/emulators.
            handler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
#endif
            return new HttpClient(handler)
            {
                BaseAddress = new Uri(apiBaseUrl),
                Timeout = TimeSpan.FromSeconds(60),
            };
        });
        builder.Services.AddSingleton<RecipeApiClient>();
        builder.Services.AddTransient<MainPage>();

        var app = builder.Build();
        embeddedSettings?.Dispose();
        return app;
    }
}
