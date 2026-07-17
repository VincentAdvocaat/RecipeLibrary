using Microsoft.AspNetCore.Http;
using RecipeLibrary.Infrastructure.Persistence;
using RecipeLibrary.Web.Middleware;
using Xunit;

namespace RecipeLibrary.Web.IntegrationTests;

public sealed class PersistenceReadinessMiddlewareTests
{
    [Fact]
    public async Task Invoke_WhenReady_CallsNext()
    {
        var readiness = new PersistenceReadiness();
        readiness.MarkReady();
        var called = false;

        var middleware = new PersistenceReadinessMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/recipes";

        await middleware.InvokeAsync(context, readiness);

        Assert.True(called);
        Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_WhenNotReady_HtmlRequest_RedirectsToStarting()
    {
        var readiness = new PersistenceReadiness();
        var middleware = new PersistenceReadinessMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/recipes";
        context.Request.Headers.Accept = "text/html";

        await middleware.InvokeAsync(context, readiness);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/starting", context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Invoke_WhenNotReady_ApiRequest_Returns503()
    {
        var readiness = new PersistenceReadiness();
        var middleware = new PersistenceReadinessMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/upload-recipe-image";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, readiness);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task Invoke_WhenPermanentlyFailed_ApiRequest_ReturnsFailedStatus()
    {
        var readiness = new PersistenceReadiness();
        readiness.MarkPermanentlyFailed();
        var middleware = new PersistenceReadinessMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/upload-recipe-image";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context, readiness);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        Assert.Contains("Failed", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Invoke_WhenNotReady_CulturePath_IsExempt()
    {
        var readiness = new PersistenceReadiness();
        var called = false;
        var middleware = new PersistenceReadinessMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/culture/set";

        await middleware.InvokeAsync(context, readiness);

        Assert.True(called);
    }

    [Fact]
    public async Task Invoke_WhenNotReady_HealthPath_IsExempt()
    {
        var readiness = new PersistenceReadiness();
        var called = false;
        var middleware = new PersistenceReadinessMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/health";

        await middleware.InvokeAsync(context, readiness);

        Assert.True(called);
    }

    [Fact]
    public async Task Invoke_WhenNotReady_HealthReadyPath_IsExempt()
    {
        var readiness = new PersistenceReadiness();
        var called = false;
        var middleware = new PersistenceReadinessMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/health/ready";

        await middleware.InvokeAsync(context, readiness);

        Assert.True(called);
    }

    [Fact]
    public async Task Invoke_WhenNotReady_StartingPath_IsExempt()
    {
        var readiness = new PersistenceReadiness();
        var called = false;
        var middleware = new PersistenceReadinessMiddleware(_ =>
        {
            called = true;
            return Task.CompletedTask;
        });

        var context = new DefaultHttpContext();
        context.Request.Path = "/starting";

        await middleware.InvokeAsync(context, readiness);

        Assert.True(called);
    }
}

public sealed class PersistenceReadinessTests
{
    [Fact]
    public void MarkReady_SetsIsReady()
    {
        var readiness = new PersistenceReadiness();
        Assert.False(readiness.IsReady);

        readiness.MarkReady();

        Assert.True(readiness.IsReady);
        Assert.False(readiness.HasPermanentlyFailed);
    }

    [Fact]
    public void MarkPermanentlyFailed_SetsFailed_WithoutReady()
    {
        var readiness = new PersistenceReadiness();
        readiness.MarkPermanentlyFailed();

        Assert.False(readiness.IsReady);
        Assert.True(readiness.HasPermanentlyFailed);
    }

    [Fact]
    public void MarkPermanentlyFailed_DoesNotDowngradeReady()
    {
        var readiness = new PersistenceReadiness();
        readiness.MarkReady();
        readiness.MarkPermanentlyFailed();

        Assert.True(readiness.IsReady);
        Assert.False(readiness.HasPermanentlyFailed);
    }
}
