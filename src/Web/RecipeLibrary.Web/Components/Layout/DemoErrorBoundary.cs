using Microsoft.AspNetCore.Components.Web;
using RecipeLibrary.Application.Abstractions;

namespace RecipeLibrary.Components.Layout;

public sealed class DemoErrorBoundary : ErrorBoundary
{
    protected override Task OnErrorAsync(Exception exception) => exception is DemoAccessException
        ? Task.CompletedTask : base.OnErrorAsync(exception);
}
