using Bunit;
using Microsoft.Extensions.DependencyInjection;

namespace RecipeLibrary.Web.ComponentTests;

public abstract class ComponentTestContext : TestContext
{
    protected ComponentTestContext()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
    }
}
