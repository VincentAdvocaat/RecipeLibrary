namespace RecipeLibrary.Application.Abstractions;

/// <summary>Server-derived identity of the current temporary visitor.</summary>
public interface IDemoContext
{
    bool IsDemo { get; }
    int Revision { get; }
}

public interface IDemoOperationRunner
{
    Task<TResult> RunReadAsync<TResult>(Func<Task<TResult>> action, CancellationToken ct);
    Task<TResult> RunAsync<TResult>(object operation, Func<Task<TResult>> action, CancellationToken ct);
}

public sealed class DemoAccessException(string code) : Exception(code)
{
    public string Code { get; } = code;
}
