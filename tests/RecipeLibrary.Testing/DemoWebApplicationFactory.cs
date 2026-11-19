using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RecipeLibrary.Web.Demo;

namespace RecipeLibrary.Testing;

/// <summary>Exercises actual Identity cookies, including antiforgery and anonymous requests.</summary>
public sealed class DemoWebApplicationFactory(string connectionString, TimeProvider? time = null,
    Action<DemoOptions>? configure = null, Action<IServiceCollection>? customize = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:RecipeDb"] = connectionString,
            ["Demo:Enabled"] = "true",
            ["Demo:StartsPerIpPerHour"] = "1000",
            ["ContentModeration:Enabled"] = "false"
        }));
        builder.ConfigureTestServices(services =>
        {
            if (time is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(time);
            }
            if (configure is not null) services.Configure(configure);
            customize?.Invoke(services);
        });
    }
}
