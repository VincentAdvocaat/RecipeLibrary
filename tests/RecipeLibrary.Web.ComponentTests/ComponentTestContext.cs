using Bunit;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RecipeLibrary.Web.Services;

namespace RecipeLibrary.Web.ComponentTests;

public abstract class ComponentTestContext : TestContext
{
    protected ComponentTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
        Services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        Services.AddScoped<MeasureSystemService>();
    }
}
