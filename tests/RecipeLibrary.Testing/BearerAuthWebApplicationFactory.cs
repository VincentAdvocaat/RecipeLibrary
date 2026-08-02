using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace RecipeLibrary.Testing;

/// <summary>
/// Web host for bearer/OpenIddict integration tests (no TestAuth override).
/// </summary>
public sealed class BearerAuthWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly string _uploadPath = Path.Combine(Path.GetTempPath(), "RecipeLibraryTests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:RecipeDb"] = connectionString,
                ["RecipeFileStorage:LocalBasePath"] = _uploadPath,
                // Stable key across requests within this factory instance.
                ["OpenIddict:SigningKey"] = "integration-test-signing-key-32bytes!!",
            });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        Directory.CreateDirectory(_uploadPath);
        return base.CreateHost(builder);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing && Directory.Exists(_uploadPath))
        {
            try
            {
                Directory.Delete(_uploadPath, recursive: true);
            }
            catch
            {
                // Best effort cleanup for temp uploads.
            }
        }
    }
}
