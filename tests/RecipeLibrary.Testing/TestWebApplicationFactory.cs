using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace RecipeLibrary.Testing;

public sealed class TestWebApplicationFactory(string connectionString) : WebApplicationFactory<Program>
{
    private readonly string _uploadPath = Path.Combine(Path.GetTempPath(), "RecipeLibraryTests", Guid.NewGuid().ToString("N"));

    public string UploadPath => _uploadPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:RecipeDb"] = connectionString,
                ["RecipeFileStorage:LocalBasePath"] = _uploadPath,
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultScheme = TestAuthHandler.SchemeName;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        Directory.CreateDirectory(_uploadPath);
        return base.CreateHost(builder);
    }

    public async Task<TestSeedData> SeedAsync(CancellationToken ct = default)
    {
        using var scope = Services.CreateScope();
        return await TestDataSeeder.SeedAsync(scope.ServiceProvider, ct);
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
